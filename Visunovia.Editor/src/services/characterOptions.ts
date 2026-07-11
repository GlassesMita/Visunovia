import { tSync } from '@/services/translationService'

export interface CharacterSelectOption {
  value: string
  text: string
}

export const characterSelectOptions: CharacterSelectOption[] = [
  { value: '', text: tSync('characterControl.noCharacter', '未选择角色') },
]

export function syncCharacterSelectOptions(characters: Array<{ id: string; name: string; displayId?: string }>) {
  characterSelectOptions.splice(
    1,
    characterSelectOptions.length - 1,
    ...characters.map(character => ({
      value: character.id,
      text: `${character.name || character.id} (${character.displayId || character.id})`,
    }))
  )
}
