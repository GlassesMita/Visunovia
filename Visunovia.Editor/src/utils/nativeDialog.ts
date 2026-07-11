import type { NativeDialogOptions } from '@/api/backendProvider'

export async function openNativeDialog(options: NativeDialogOptions): Promise<string | null> {
  if (!window.visunoviaDesktop?.openDialog) {
    throw new Error('系统文件选择器仅在桌面版中可用')
  }
  return window.visunoviaDesktop.openDialog(options)
}