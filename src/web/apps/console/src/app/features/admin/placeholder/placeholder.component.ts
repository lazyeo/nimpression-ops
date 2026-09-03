import { Component, ChangeDetectionStrategy, inject, computed } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';

@Component({
  selector: 'nim-placeholder',
  standalone: true,
  imports: [RouterLink, I18nPipe, IconComponent],
  templateUrl: './placeholder.component.html',
  styleUrls: ['./placeholder.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlaceholderComponent {
  private readonly route = inject(ActivatedRoute);

  readonly labelKey = computed(() => {
    return (this.route.snapshot.data?.['labelKey'] as string) || 'ADMIN.PLACEHOLDER_TITLE';
  });

  readonly iconName = computed(() => {
    return (this.route.snapshot.data?.['icon'] as string) || 'construction';
  });
}
