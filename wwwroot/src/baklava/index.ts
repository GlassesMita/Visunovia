import { Editor } from '@baklavajs/core'
import { DependencyEngine } from '@baklavajs/engine'
import { BaklavaInterfaceTypes, NodeInterfaceType } from '@baklavajs/interface-types'

export function createBaklavaEditor(): Editor {
  const editor = new Editor()

  const intfTypes = new BaklavaInterfaceTypes(editor)
  const numberType = new NodeInterfaceType<number>('number')
  const stringType = new NodeInterfaceType<string>('string')
  const execType = new NodeInterfaceType<void>('exec')

  intfTypes.addTypes(numberType, stringType, execType)

  new DependencyEngine(editor)

  return editor
}
