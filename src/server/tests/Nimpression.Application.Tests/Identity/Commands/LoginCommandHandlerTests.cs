using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.Commands.Login;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Commands;

/// <summary>
/// 登录命令处理程序单元测试。
/// 
/// 时序侧信道保障策略说明（R1 / R3）：
/// 登录接口通过预置假 BCrypt 校验（DummyBcryptHash）来对齐「用户不存在」与「密码错误」两类路径的耗时，
/// 消除攻击者通过测量响应时间枚举有效邮箱的时序侧信道漏洞（Timing Oracle）。
/// 
/// 主保障采用【接缝测试】（Seam Testing）：
/// 1. 使用 NSubstitute 替身 <see cref="IPasswordHasher"/>，直接断言内部 <see cref="IPasswordHasher.VerifyPassword"/> 是否被精确调用 1 次；
/// 2. 验证两类路径返回的失败结果完全不可区分（统一错误码 AUTH_INVALID_CREDENTIALS 与统一文案）；
/// 3. 接缝测试毫秒级运行、完全确定性、免疫机器 CPU 负载波动，直接验证缓解代码的执行；
/// 4. 挂钟耗时集成测试已隔离至 Timing Category，仅在低负载基准环境中执行。
/// </summary>
public class LoginCommandHandlerTests
{
    private readonly IIdentityRepository _identityRepository = Substitute.For<IIdentityRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditSink _auditSink = Substitute.For<IAuditSink>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly LoginCommandHandler _handler;
    private readonly DateTimeOffset _now = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    public LoginCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(_now);
        _handler = new LoginCommandHandler(
            _identityRepository,
            _passwordHasher,
            _jwtTokenGenerator,
            _unitOfWork,
            _auditSink,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsLoginResultDtoAndAddsRefreshToken()
    {
        // Arrange
        var email = new EmailAddress("driver@example.com");
        var user = new User(
            Guid.NewGuid(),
            email,
            "hashed_password",
            UserRole.Driver,
            "John Driver");

        _identityRepository.GetUserByEmailAsync(Arg.Is<EmailAddress>(e => e.Value == email.Value), Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("ValidPassword123!", "hashed_password")
            .Returns(true);

        _jwtTokenGenerator.GenerateAccessToken(user.Id, email.Value, "Driver", "John Driver")
            .Returns(("access_token_123", 900));

        _jwtTokenGenerator.GenerateRefreshToken(Arg.Any<string?>())
            .Returns(("raw_refresh_token", "hash_refresh_token", _now.AddDays(7)));

        var command = new LoginCommand("driver@example.com", "ValidPassword123!", "127.0.0.1", "Mozilla/5.0");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("access_token_123");
        result.Value.RawRefreshToken.Should().Be("raw_refresh_token");
        result.Value.ExpiresIn.Should().Be(900);
        result.Value.User.Email.Should().Be("driver@example.com");
        result.Value.User.DisplayName.Should().Be("John Driver");
        result.Value.User.Role.Should().Be(UserRole.Driver);

        await _identityRepository.Received(1).AddRefreshTokenAsync(
            Arg.Is<RefreshToken>(rt => rt.UserId == user.Id && rt.TokenHash == "hash_refresh_token"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ReturnsUnauthorizedAndExecutesDummyPasswordVerification()
    {
        // Arrange
        _identityRepository.GetUserByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var command = new LoginCommand("nonexistent@example.com", "Password123!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
        result.Error.Message.Should().Be("Invalid email or password.");

        // 验证对预置的假 BCrypt 哈希执行了密码比对，且恰好调用 1 次，消除时序侧信道
        _passwordHasher.Received(1).VerifyPassword(
            "Password123!",
            Arg.Is<string>(s => s.StartsWith("$2a$12$", StringComparison.Ordinal)));
        _passwordHasher.Received(1).VerifyPassword(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ReturnsUnauthorizedWithSameUnifiedMessage()
    {
        // Arrange
        var email = new EmailAddress("admin@example.com");
        var user = new User(Guid.NewGuid(), email, "hashed_password", UserRole.Admin, "Admin User");

        _identityRepository.GetUserByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("WrongPassword123!", "hashed_password")
            .Returns(false);

        var command = new LoginCommand("admin@example.com", "WrongPassword123!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
        result.Error.Message.Should().Be("Invalid email or password.");
        user.FailedLoginAttempts.Should().Be(1);

        // 验证对真实密码哈希执行了密码比对，且恰好调用 1 次
        _passwordHasher.Received(1).VerifyPassword("WrongPassword123!", "hashed_password");
        _passwordHasher.Received(1).VerifyPassword(Arg.Any<string>(), Arg.Any<string>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TimingSideChannel_NonExistentEmailAndWrongPassword_BothExecuteVerifyPasswordExactlyOnce_AndProduceIndistinguishableErrors()
    {
        // Arrange 1: 用户不存在的 handler 与替身
        var repoNonExistent = Substitute.For<IIdentityRepository>();
        var hasherNonExistent = Substitute.For<IPasswordHasher>();
        var uowNonExistent = Substitute.For<IUnitOfWork>();
        var auditNonExistent = Substitute.For<IAuditSink>();
        var jwtNonExistent = Substitute.For<IJwtTokenGenerator>();
        var timeNonExistent = Substitute.For<IDateTimeProvider>();
        timeNonExistent.UtcNow.Returns(_now);

        repoNonExistent.GetUserByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handlerNonExistent = new LoginCommandHandler(
            repoNonExistent,
            hasherNonExistent,
            jwtNonExistent,
            uowNonExistent,
            auditNonExistent,
            timeNonExistent);

        // Arrange 2: 用户存在但密码错误的 handler 与替身
        var repoWrongPassword = Substitute.For<IIdentityRepository>();
        var hasherWrongPassword = Substitute.For<IPasswordHasher>();
        var uowWrongPassword = Substitute.For<IUnitOfWork>();
        var auditWrongPassword = Substitute.For<IAuditSink>();
        var jwtWrongPassword = Substitute.For<IJwtTokenGenerator>();
        var timeWrongPassword = Substitute.For<IDateTimeProvider>();
        timeWrongPassword.UtcNow.Returns(_now);

        var existingUser = new User(Guid.NewGuid(), new EmailAddress("existing@example.com"), "real_bcrypt_hash", UserRole.Driver, "Driver");
        repoWrongPassword.GetUserByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(existingUser);
        hasherWrongPassword.VerifyPassword("WrongPass!", "real_bcrypt_hash")
            .Returns(false);

        var handlerWrongPassword = new LoginCommandHandler(
            repoWrongPassword,
            hasherWrongPassword,
            jwtWrongPassword,
            uowWrongPassword,
            auditWrongPassword,
            timeWrongPassword);

        // Act
        var resultNonExistent = await handlerNonExistent.Handle(
            new LoginCommand("nonexistent@example.com", "WrongPass!", "127.0.0.1"),
            CancellationToken.None);

        var resultWrongPassword = await handlerWrongPassword.Handle(
            new LoginCommand("existing@example.com", "WrongPass!", "127.0.0.1"),
            CancellationToken.None);

        // Assert 1: 两条路径均失败
        resultNonExistent.IsSuccess.Should().BeFalse();
        resultWrongPassword.IsSuccess.Should().BeFalse();

        // Assert 2: VerifyPassword 均恰好被调用 1 次
        hasherNonExistent.Received(1).VerifyPassword(
            "WrongPass!",
            Arg.Is<string>(h => h.StartsWith("$2a$12$", StringComparison.Ordinal)));
        hasherNonExistent.Received(1).VerifyPassword(Arg.Any<string>(), Arg.Any<string>());

        hasherWrongPassword.Received(1).VerifyPassword("WrongPass!", "real_bcrypt_hash");
        hasherWrongPassword.Received(1).VerifyPassword(Arg.Any<string>(), Arg.Any<string>());

        // Assert 3: 两种情况返回的失败结果必须完全不可区分（相同错误码与错误信息，不得泄露用户是否存在）
        resultNonExistent.Error.Should().NotBeNull();
        resultWrongPassword.Error.Should().NotBeNull();
        resultNonExistent.Error!.Code.Should().Be(resultWrongPassword.Error!.Code);
        resultNonExistent.Error.Message.Should().Be(resultWrongPassword.Error.Message);
        resultNonExistent.Error.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
        resultNonExistent.Error.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_With5FailedAttempts_LocksAccountAndRecordsAuditEvent()
    {
        // Arrange
        var email = new EmailAddress("user@example.com");
        var user = new User(Guid.NewGuid(), email, "hashed_password", UserRole.Dispatcher, "Dispatcher");
        user.RecordLoginFailure(_now);
        user.RecordLoginFailure(_now);
        user.RecordLoginFailure(_now);
        user.RecordLoginFailure(_now); // 4 failures already

        _identityRepository.GetUserByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("WrongPassword!", "hashed_password")
            .Returns(false);

        var command = new LoginCommand("user@example.com", "WrongPassword!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        user.IsLockedOut(_now).Should().BeTrue();
        user.LockoutEnd.Should().Be(_now.AddMinutes(15));

        await _auditSink.Received(1).RecordAsync(
            Arg.Is<string>(e => e == "User"),
            Arg.Is<Guid?>(id => id == user.Id),
            Arg.Is<string>(a => a == "User.Lockout"),
            Arg.Is<string?>(b => b == null),
            Arg.Is<string?>(s => s != null && s.Contains("5 consecutive failed login attempts")),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAccountIsLockedOut_WithCorrectPassword_ReturnsUnauthorizedWithLockedOutError()
    {
        // Arrange
        var email = new EmailAddress("locked@example.com");
        var user = new User(Guid.NewGuid(), email, "hashed_password", UserRole.Driver, "Locked Driver");
        for (var i = 0; i < 5; i++)
        {
            user.RecordLoginFailure(_now);
        }

        _identityRepository.GetUserByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("CorrectPassword123!", "hashed_password")
            .Returns(true);

        var command = new LoginCommand("locked@example.com", "CorrectPassword123!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: 密码正确但处于锁定状态，返回明确的锁定提示
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("AUTH_LOCKED_OUT");
        _passwordHasher.Received(1).VerifyPassword("CorrectPassword123!", "hashed_password");
    }

    [Fact]
    public async Task Handle_WhenAccountIsLockedOut_WithWrongPassword_ReturnsInvalidCredentials_WithoutLeakingLockout()
    {
        // Arrange
        var email = new EmailAddress("locked@example.com");
        var user = new User(Guid.NewGuid(), email, "hashed_password", UserRole.Driver, "Locked Driver");
        for (var i = 0; i < 5; i++)
        {
            user.RecordLoginFailure(_now);
        }

        _identityRepository.GetUserByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("WrongPassword123!", "hashed_password")
            .Returns(false);

        var command = new LoginCommand("locked@example.com", "WrongPassword123!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: 密码错误时不透露锁定状态，统一返回 AUTH_INVALID_CREDENTIALS
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenAccountIsInactive_ReturnsForbidden()
    {
        // Arrange
        var email = new EmailAddress("inactive@example.com");
        var user = new User(Guid.NewGuid(), email, "hashed_password", UserRole.Driver, "Inactive Driver");
        user.SetStatus(UserStatus.Inactive);

        _identityRepository.GetUserByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("Password123!", "hashed_password")
            .Returns(true);

        var command = new LoginCommand("inactive@example.com", "Password123!", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("AUTH_ACCOUNT_INACTIVE");
    }
}
