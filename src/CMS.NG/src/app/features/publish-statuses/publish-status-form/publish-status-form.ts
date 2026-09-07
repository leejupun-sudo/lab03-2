import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';

import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';
import { PublishStatusService } from '@core/services/publish-status.service';

/** `pkid` is a tinyint — SQL accepts 0..255, but 0 is reserved as the "not supplied" sentinel. */
const MIN_PKID = 1;
const MAX_PKID = 255;

@Component({
  selector: 'app-publish-status-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
  ],
  templateUrl: './publish-status-form.html',
  styleUrl: './publish-status-form.scss',
})
export class PublishStatusForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly pkid = signal(0);

  protected readonly minPkid = MIN_PKID;
  protected readonly maxPkid = MAX_PKID;

  protected readonly form = this.fb.nonNullable.group({
    pkid: [
      null as number | null,
      [Validators.required, Validators.min(MIN_PKID), Validators.max(MAX_PKID)],
    ],
    description: ['', [Validators.required, Validators.maxLength(50)]],
    isDraft: [false],
    isPublished: [false],
    isDiscontinued: [false],
  });

  protected get isEdit(): boolean {
    return this.pkid() > 0;
  }

  protected get title(): string {
    return this.isEdit ? '編輯發布狀態' : '新增發布狀態';
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const pkid = idParam ? Number(idParam) : 0;
    this.pkid.set(Number.isFinite(pkid) ? pkid : 0);

    if (!this.isEdit) {
      // Nothing to load — this form has no FK lookups.
      return;
    }

    this.loading.set(true);
    this.service.getById(this.pkid()).subscribe({
      next: (status) => {
        this.patchForm(status);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入發布狀態資料。',
        });
      },
    });
  }

  private patchForm(status: PublishStatus): void {
    this.form.patchValue({
      pkid: status.pkid,
      description: status.description,
      isDraft: status.isDraft,
      isPublished: status.isPublished,
      isDiscontinued: status.isDiscontinued,
    });
    // pkid is the key Course and Promotion2 reference — immutable once created.
    this.form.controls.pkid.disable();
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.messageService.add({
        severity: 'warn',
        summary: '欄位有誤',
        detail: '請修正紅字標示的欄位後再儲存。',
      });
      return;
    }

    const value = this.form.getRawValue();
    const request: PublishStatusRequest = {
      pkid: value.pkid ?? 0,
      description: value.description.trim(),
      isDraft: value.isDraft,
      isPublished: value.isPublished,
      isDiscontinued: value.isDiscontinued,
    };

    this.saving.set(true);
    const request$ = this.isEdit ? this.service.update(request) : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEdit ? '更新成功' : '新增成功',
          detail: `發布狀態「${saved.description}」已儲存。`,
        });
        void this.router.navigate(['/publish-statuses', saved.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error.status === 409
              ? `主代碼「${request.pkid}」已存在。`
              : '儲存發布狀態時發生錯誤。',
        });
      },
    });
  }

  protected cancel(): void {
    void this.router.navigate(
      this.isEdit ? ['/publish-statuses', this.pkid()] : ['/publish-statuses'],
    );
  }
}
