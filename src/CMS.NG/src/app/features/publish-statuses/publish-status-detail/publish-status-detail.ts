import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';

import { PublishStatus } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';

@Component({
  selector: 'app-publish-status-detail',
  imports: [RouterLink, ButtonModule, TagModule],
  templateUrl: './publish-status-detail.html',
  styleUrl: './publish-status-detail.scss',
})
export class PublishStatusDetail implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly status = signal<PublishStatus | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!pkid) {
      this.loading.set(false);
      return;
    }

    // No lookups to load — PublishStatus has no FK columns.
    this.service.getById(pkid).subscribe({
      next: (status) => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: `找不到主代碼 ${pkid} 的發布狀態。`,
        });
      },
    });
  }

  /** True when nothing references this status — it is then safe to delete. */
  protected get isUnused(): boolean {
    const status = this.status();
    return !!status && status.courseCount === 0 && status.promotion2Count === 0;
  }

  protected goBack(): void {
    void this.router.navigate(['/publish-statuses']);
  }
}
