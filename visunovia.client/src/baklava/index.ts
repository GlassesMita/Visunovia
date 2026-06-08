import { Editor } from '@baklavajs/core'
import { DependencyEngine } from '@baklavajs/engine'
import { BaklavaInterfaceTypes, NodeInterfaceType } from '@baklavajs/interface-types'
import type { IConnection } from '@baklavajs/core'
import type { IAddConnectionEventData } from '@baklavajs/core'

export function createBaklavaEditor(): Editor {
  const editor = new Editor()

  const intfTypes = new BaklavaInterfaceTypes(editor)
  const numberType = new NodeInterfaceType<number>('number')
  const stringType = new NodeInterfaceType<string>('string')
  const execType = new NodeInterfaceType<void>('exec')

  intfTypes.addTypes(numberType, stringType, execType)

  new DependencyEngine(editor)

  setupEventListeners(editor)

  return editor
}

function setupEventListeners(editor: Editor): void {
  const graph = editor.graph

  const eventToken = {}

  graph.events.addConnection.subscribe(eventToken, (connection: IConnection) => {
    console.log('[BaklavaEvents] 连接已建立:', {
      id: connection.id,
      from: `${connection.from.nodeId}:${connection.from.id}`,
      to: `${connection.to.nodeId}:${connection.to.id}`,
    })
  })

  graph.events.removeConnection.subscribe(eventToken, (connection: IConnection) => {
    console.log('[BaklavaEvents] 连接已断开:', {
      id: connection.id,
      from: `${connection.from.nodeId}:${connection.from.id}`,
      to: `${connection.to.nodeId}:${connection.to.id}`,
    })
  })

  graph.events.beforeAddConnection.subscribe(eventToken, (data: IAddConnectionEventData) => {
    console.log('[BaklavaEvents] 即将建立连接:', {
      from: `${data.from.nodeId}:${data.from.id}`,
      to: `${data.to.nodeId}:${data.to.id}`,
    })
  })

  console.log('[BaklavaEvents] 事件监听器已初始化')
}
