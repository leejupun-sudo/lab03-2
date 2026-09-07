import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';

import { CourseGroup } from '@core/models/course-group.model';
import { CourseGroupService } from '@core/services/course-group.service';

@Component({
  selector: 'app-course-group-detail',
  imports: [RouterLink, ButtonModule],
  templateUrl: './course-group-detail.html',
  styleUrl: './course-group-detail.scss',
})
export class CourseGroupDetail implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly group = signal<CourseGroup | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!pkid) {
      this.loading.set(false);
      return;
    }

    // No lookups to load — CourseGroup has no FK columns.
    this.service.getById(pkid).subscribe({
      next: (group) => {
        this.group.set(group);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: `找不到主代碼 ${pkid} 的課程群組。`,
        });
      },
    });
  }

  /** True when nothing references this group — it is then safe to delete. */
  protected get isUnused(): boolean {
    const group = this.group();
    return !!group && group.courseCount === 0 && group.partnerCourseGroupCount === 0;
  }

  protected goBack(): void {
    void this.router.navigate(['/course-groups']);
  }
}
