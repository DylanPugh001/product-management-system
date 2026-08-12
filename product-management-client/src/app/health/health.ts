import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HealthService, HealthStatus } from '../health.service';

@Component({
  selector: 'app-health',
  imports: [],
  templateUrl: './health.html',
  styleUrl: './health.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HealthComponent {
  private readonly healthService = inject(HealthService);

  protected readonly status = signal<HealthStatus | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  constructor() {
    this.healthService.getHealth().subscribe({
      next: (health) => {
        this.status.set(health);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(
          `Failed to reach the API: ${err.message ?? err.statusText ?? 'unknown error'}`,
        );
        this.loading.set(false);
      },
    });
  }
}
