export type CustomEventCommand = {
  target: 'event' | 'extension'
  name: string
  args: Array<string | number | boolean>
}

const SUPPORTED_NAMES = new Set([
  'CreateWindow',
  'CloseWindow',
  'ShowWindow',
  'HideWindow',
  'MoveWindow',
  'ResizeWindow',
  'SetWindowAlwaysOnTop',
  'ChangeBackground',
  'PlayBgm',
  'StopBgm',
  'PlaySfx',
  'PlayVoice',
  'ShowCharacter',
  'HideCharacter',
  'FadeScreen',
])

export function parseCustomEventScript(source: unknown): CustomEventCommand[] {
  const text = String(source || '').trim()
  if (!text) return []

  const commands: CustomEventCommand[] = []
  const invocationPattern = /\b(Event|Extension)\.([A-Za-z][A-Za-z0-9_]*)\s*\(([^()]*)\)\s*;/g
  let match: RegExpExecArray | null
  while ((match = invocationPattern.exec(text))) {
    const target = match[1].toLowerCase() === 'event' ? 'event' : 'extension'
    const name = match[2]
    if (target === 'event' && !SUPPORTED_NAMES.has(name)) {
      throw new Error(`Unsupported Event call: ${name}`)
    }
    commands.push({ target, name, args: parseArguments(match[3]) })
  }
  if (commands.length === 0) {
    throw new Error('Custom event script must contain an Event.* or Extension.* call')
  }
  return commands
}

function parseArguments(input: string): Array<string | number | boolean> {
  if (!input.trim()) return []
  const values: Array<string | number | boolean> = []
  const parts = input.match(/(?:"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'|[^,])+/g) || []
  for (const rawPart of parts) {
    const value = rawPart.trim()
    if (/^".*"$|^'.*'$/.test(value)) {
      values.push(value.slice(1, -1).replace(/\\([\\"'])/g, '$1'))
    } else if (value === 'true' || value === 'false') {
      values.push(value === 'true')
    } else {
      const numberValue = Number(value)
      if (!Number.isFinite(numberValue)) throw new Error(`Invalid custom event argument: ${value}`)
      values.push(numberValue)
    }
  }
  return values
}