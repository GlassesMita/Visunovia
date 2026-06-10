import { Editor } from '@baklavajs/core'
import { DependencyEngine } from '@baklavajs/engine'
import { BaklavaInterfaceTypes, NodeInterfaceType } from '@baklavajs/interface-types'
import type { IConnection } from '@baklavajs/core'

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

  function getNodeType(node: any) {
    return String(node?.type || node?.constructor?.type || node?.constructor?.name || '')
  }

  function findInterfaceKey(interfaces: Record<string, any> | undefined, iface: any, fallback?: string) {
    if (!interfaces || !iface) return fallback || ''
    const entry = Object.entries(interfaces).find(([, candidate]) => candidate === iface)
    return entry?.[0] || fallback || ''
  }

  function setInterfaceValue(iface: any, value: string) {
    if (!iface) return
    if (typeof iface.setValue === 'function') {
      iface.setValue(value)
    } else {
      iface.value = value
    }
  }

  function syncCharacterControlSlot(connection: IConnection) {
    const sourceNode = graph.nodes.find(node => node.id === connection.from.nodeId) as any
    const targetNode = graph.nodes.find(node => node.id === connection.to.nodeId) as any
    if (getNodeType(sourceNode) !== 'CharacterControlNode' || getNodeType(targetNode) !== 'DialogueNode') return

    const targetPort = findInterfaceKey(targetNode.inputs, connection.to, connection.to.name)
    const slotMatch = String(targetPort || '').match(/^characterControl(\d+)$/)
    if (!slotMatch) return

    const slot = slotMatch[1]
    setInterfaceValue(sourceNode.inputs?.slot, slot)
    if (slot === '6') {
      setInterfaceValue(sourceNode.inputs?.sprite, '')
    }
  }

  graph.events.addConnection.subscribe(eventToken, (connection: IConnection) => {
    syncCharacterControlSlot(connection)
  })

}
