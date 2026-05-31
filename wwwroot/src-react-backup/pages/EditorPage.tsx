import AppLayout from '@/components/layout/AppLayout'
import NodeGraphEditor from '@/components/node-graph/NodeGraphEditor'
import { useLocalization } from '@/hooks/useLocalization'

/**
 * 编辑器主页面：组合 AppLayout 布局和 NodeGraphEditor 画布。
 * 由 React Router 渲染为根路径（/）的子路由内容。
 */
export default function EditorPage() {
  const { t } = useLocalization()

  return (
    <AppLayout>
      <NodeGraphEditor />
    </AppLayout>
  )
}