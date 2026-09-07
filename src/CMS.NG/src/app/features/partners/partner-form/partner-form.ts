import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';

import {
  PARTNER_DISPLAY_ORDER_LAST,
  Partner,
  PartnerRequest,
} from '@core/models/partner.model';
import { PartnerService } from '@core/services/partner.service';

@Component({
  selector: 'app-partner-form',
  imports: [ReactiveFormsModule, ButtonModule, InputNumberModule, InputTextModule],
  templateUrl: './partner-form.html',
  styleUrl: './partner-form.scss',
})
export class PartnerForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly pkid = signal(0);

  protected readonly minDisplayOrder = 0;
  protected readonly maxDisplayOrder = PARTNER_DISPLAY_ORDER_LAST;

  // pkid is IDENTITY — never an input, so the form holds only the writable columns.
  // ImageFilename has no format rule: 17 of the 62 non-null live values carry no extension.
  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(50)]],
    appKey: ['', [Validators.required, Validators.maxLength(10)]],
    nameOnPartnerMenu: ['', [Validators.required, Validators.maxLength(200)]],
    nameOnCourseDetailPage: ['', [Validators.required, Validators.maxLength(50)]],
    displayOrder: [
      PARTNER_DISPLAY_ORDER_LAST,
      [Validators.required, Validators.min(0), Validators.max(PARTNER_DISPLAY_ORDER_LAST)],
    ],
    imageFilename: ['', [Validators.maxLength(50)]],
  });

  protected get isEdit(): boolean {
    return this.pkid() > 0;
  }

  protected get title(): string {
    return this.isEdit ? '編輯合作廠商' : '新增合作廠商';
  }

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const pkid = idParam ? Number(idParam) : 0;
    this.pkid.set(Number.isFinite(pkid) ? pkid : 0);

    if (!this.isEdit) {
      // Nothing to load — this form has no FK lookups. A new partner defaults to
      // DisplayOrder 9999 so it parks at the end of the menu rather than jumping to 0.
      return;
    }

    this.loading.set(true);
    this.service.getById(this.pkid()).subscribe({
      next: (partner) => {
        this.patchForm(partner);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入合作廠商資料。',
        });
      },
    });
  }

  private patchForm(partner: Partner): void {
    this.form.patchValue({
      name: partner.name,
      appKey: partner.appKey,
      nameOnPartnerMenu: partner.nameOnPartnerMenu,
      nameOnCourseDetailPage: partner.nameOnCourseDetailPage,
      displayOrder: partner.displayOrder,
      imageFilename: partner.imageFilename ?? '',
    });
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
    const imageFilename = value.imageFilename.trim();
    const request: PartnerRequest = {
      pkid: this.pkid(),
      name: value.name.trim(),
      appKey: value.appKey.trim(),
      nameOnPartnerMenu: value.nameOnPartnerMenu.trim(),
      nameOnCourseDetailPage: value.nameOnCourseDetailPage.trim(),
      displayOrder: value.displayOrder,
      // The column holds NULLs, never empty strings.
      imageFilename: imageFilename === '' ? null : imageFilename,
    };

    this.saving.set(true);
    const request$ = this.isEdit ? this.service.update(request) : this.service.create(request);

    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEdit ? '更新成功' : '新增成功',
          detail: `合作廠商「${saved.name}」已儲存。`,
        });
        void this.router.navigate(['/partners', saved.pkid]);
      },
      // Name carries no uniqueness rule; AppKey does — the only 409 on save.
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail:
            error.status === 409
              ? `應用代碼「${request.appKey}」已被其他廠商使用。`
              : '儲存合作廠商時發生錯誤。',
        });
      },
    });
  }

  protected cancel(): void {
    void this.router.navigate(this.isEdit ? ['/partners', this.pkid()] : ['/partners']);
  }
}
