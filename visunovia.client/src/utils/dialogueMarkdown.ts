import MarkdownIt from 'markdown-it'

const markdownRenderer = new MarkdownIt({
  breaks: true,
  html: true,
  linkify: true,
  typographer: true,
})

markdownRenderer.enable(['table', 'strikethrough'])
markdownRenderer.inline.ruler.before('link', 'visunovia_ruby_annotation', createRubyAnnotationRule())
markdownRenderer.inline.ruler.before('link', 'visunovia_inside_annotation', createInsideAnnotationRule())
markdownRenderer.inline.ruler.before('emphasis', 'visunovia_mark', createSimpleInlineRule('==', 'mark'))
markdownRenderer.inline.ruler.before('emphasis', 'visunovia_underline', createSimpleInlineRule('++', 'u'))

export function renderDialogueMarkdown(value: string, visibleLimit = Number.POSITIVE_INFINITY) {
  const rendered = renderDialogueInlineMarkup(normalizeDialogueEscapes(value), visibleLimit)
  return markdownRenderer
    .render(rendered.html)
    .replace(/<br\s*\/?>\s*\n/gi, '<br />')
}

export function renderDialogueInlineMarkup(value: string, visibleLimit = Number.POSITIVE_INFINITY) {
  const initialRemaining = Number.isFinite(visibleLimit) ? visibleLimit : Number.MAX_SAFE_INTEGER
  const state = { remaining: initialRemaining }
  return { html: renderInlineSegment(value, state), consumed: initialRemaining - state.remaining }
}

export function normalizeDialogueEscapes(value: string) {
  return value.replace(/\\([nrt\\])/g, (_, escaped: string) => {
    const replacements: Record<string, string> = {
      n: '<br />',
      r: '',
      t: '&emsp;',
      '\\': '\\',
    }
    return replacements[escaped] ?? escaped
  })
}

export function countDialogueVisibleCharacters(value: string) {
  return renderDialogueInlineMarkup(normalizeDialogueEscapes(value), Number.POSITIVE_INFINITY).consumed
}

export function stripHtml(value: string) {
  return value.replace(/<[^>]*>/g, '')
}

function createSimpleInlineRule(marker: string, tag: string) {
  return (state: any, silent: boolean) => {
    const start = state.pos
    if (!state.src.startsWith(marker, start)) return false

    const contentStart = start + marker.length
    const end = state.src.indexOf(marker, contentStart)
    if (end < 0 || end === contentStart) return false

    if (!silent) {
      const tokenOpen = state.push(`${tag}_open`, tag, 1)
      tokenOpen.markup = marker
      const textToken = state.push('text', '', 0)
      textToken.content = state.src.slice(contentStart, end)
      const tokenClose = state.push(`${tag}_close`, tag, -1)
      tokenClose.markup = marker
    }

    state.pos = end + marker.length
    return true
  }
}

function createRubyAnnotationRule() {
  return (state: any, silent: boolean) => {
    const start = state.pos
    const marker = '[Ann|'
    if (!state.src.startsWith(marker, start)) return false

    const contentStart = start + marker.length
    const end = state.src.indexOf(']', contentStart)
    if (end < 0) return false

    const content = state.src.slice(contentStart, end)
    const separator = content.indexOf('|')
    if (separator < 0) return false

    const mainText = content.slice(0, separator)
    const annotationText = content.slice(separator + 1)
    if (!mainText || !annotationText) return false

    if (!silent) {
      state.push('ruby_open', 'ruby', 1)
      const mainToken = state.push('text', '', 0)
      mainToken.content = mainText
      state.push('rp_open', 'rp', 1)
      const leftRp = state.push('text', '', 0)
      leftRp.content = '('
      state.push('rp_close', 'rp', -1)
      state.push('rt_open', 'rt', 1)
      const annotationToken = state.push('text', '', 0)
      annotationToken.content = annotationText
      state.push('rt_close', 'rt', -1)
      state.push('rp_open', 'rp', 1)
      const rightRp = state.push('text', '', 0)
      rightRp.content = ')'
      state.push('rp_close', 'rp', -1)
      state.push('ruby_close', 'ruby', -1)
    }

    state.pos = end + 1
    return true
  }
}

function createInsideAnnotationRule() {
  return (state: any, silent: boolean) => {
    const start = state.pos
    const marker = '[Inside|'
    if (!state.src.startsWith(marker, start)) return false

    const contentStart = start + marker.length
    const end = state.src.indexOf(']', contentStart)
    if (end < 0) return false

    const content = state.src.slice(contentStart, end)
    const separator = content.indexOf('|')
    const hiddenText = separator >= 0 ? content.slice(0, separator) : content
    const titleText = (separator >= 0 ? content.slice(separator + 1) : '').trim() || '你知道的太多了'
    if (!hiddenText) return false

    if (!silent) {
      const openToken = state.push('span_open', 'span', 1)
      openToken.attrs = [
        ['class', 'dialog-inside'],
        ['title', titleText],
      ]
      const textToken = state.push('text', '', 0)
      textToken.content = hiddenText
      state.push('span_close', 'span', -1)
    }

    state.pos = end + 1
    return true
  }
}

function renderInlineSegment(value: string, state: { remaining: number }) {
  let output = ''
  let plain = ''
  let index = 0

  const flushPlain = () => {
    if (!plain) return
    output += markdownRenderer.renderInline(plain)
    plain = ''
  }

  while (index < value.length && state.remaining > 0) {
    const simple = parseSimpleMarkup(value, index, state)
    if (simple) {
      flushPlain()
      output += simple.html
      index = simple.end
      continue
    }

    const annotation = parseAnnotationMarkup(value, index, state)
    if (annotation) {
      flushPlain()
      output += annotation.html
      index = annotation.end
      continue
    }

    const char = readCodePoint(value, index)
    plain += char.value
    state.remaining -= 1
    index = char.end
  }

  flushPlain()
  return output
}

function parseSimpleMarkup(value: string, start: number, state: { remaining: number }) {
  const config = value.startsWith('==', start)
    ? { marker: '==', tag: 'mark' }
    : value.startsWith('++', start)
      ? { marker: '++', tag: 'u' }
      : null
  if (!config) return null

  const contentStart = start + config.marker.length
  const end = findClosingMarker(value, contentStart, config.marker)
  if (end < 0) return null

  const inner = renderInlineSegment(value.slice(contentStart, end), state)
  return {
    html: inner ? `<${config.tag}>${inner}</${config.tag}>` : '',
    end: end + config.marker.length,
  }
}

function parseAnnotationMarkup(value: string, start: number, state: { remaining: number }) {
  const type = value.startsWith('[Ann|', start) ? 'ann' : value.startsWith('[Inside|', start) ? 'inside' : ''
  if (!type) return null

  const contentStart = start + (type === 'ann' ? '[Ann|'.length : '[Inside|'.length)
  const end = findClosingBracket(value, contentStart)
  if (end < 0) return null

  const parts = splitTopLevelPipes(value.slice(contentStart, end))
  if (type === 'ann') {
    if (parts.length < 2 || !parts[0] || !parts[1]) return null
    const before = state.remaining
    const mainHtml = renderInlineSegment(parts[0], state)
    if (!mainHtml && before === state.remaining) return { html: '', end: end + 1 }
    const annotationHtml = renderInlineSegment(parts.slice(1).join('|'), { remaining: Number.POSITIVE_INFINITY })
    return {
      html: `<ruby>${mainHtml}<rp>(</rp><rt>${annotationHtml}</rt><rp>)</rp></ruby>`,
      end: end + 1,
    }
  }

  if (!parts[0]) return null
  const before = state.remaining
  const hiddenHtml = renderInlineSegment(parts[0], state)
  if (!hiddenHtml && before === state.remaining) return { html: '', end: end + 1 }
  const title = stripHtml(renderInlineSegment(parts.slice(1).join('|') || '你知道的太多了', { remaining: Number.POSITIVE_INFINITY }))
  return {
    html: `<span class="dialog-inside" title="${escapeHtmlAttribute(title)}">${hiddenHtml}</span>`,
    end: end + 1,
  }
}

function findClosingMarker(value: string, start: number, marker: string) {
  let index = start
  while (index < value.length) {
    if (value.startsWith(marker, index)) return index
    const char = readCodePoint(value, index)
    index = char.end
  }
  return -1
}

function findClosingBracket(value: string, start: number) {
  let depth = 0
  let index = start
  while (index < value.length) {
    if (value.startsWith('[Ann|', index) || value.startsWith('[Inside|', index)) {
      depth += 1
      index += value.startsWith('[Ann|', index) ? '[Ann|'.length : '[Inside|'.length
      continue
    }
    if (value[index] === ']') {
      if (depth === 0) return index
      depth -= 1
    }
    index += 1
  }
  return -1
}

function splitTopLevelPipes(value: string) {
  const parts: string[] = []
  let depth = 0
  let start = 0
  let index = 0
  while (index < value.length) {
    if (value.startsWith('[Ann|', index) || value.startsWith('[Inside|', index)) {
      depth += 1
      index += value.startsWith('[Ann|', index) ? '[Ann|'.length : '[Inside|'.length
      continue
    }
    if (value[index] === ']' && depth > 0) depth -= 1
    if (value[index] === '|' && depth === 0) {
      parts.push(value.slice(start, index))
      start = index + 1
    }
    index += 1
  }
  parts.push(value.slice(start))
  return parts
}

function readCodePoint(value: string, start: number) {
  const codePoint = value.codePointAt(start)
  const char = codePoint === undefined ? '' : String.fromCodePoint(codePoint)
  return { value: char, end: start + char.length }
}

function escapeHtmlAttribute(value: string) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
}
