const fs = require('fs');
const path = require('path');

const filePath = path.join(__dirname, 'src/stores/useLocalizationStore.ts');
const content = fs.readFileSync(filePath, 'utf-8');

const result = [
  'SIZE: ' + content.length,
  'HAS_NEW_PATH: ' + content.includes('localization/translations'),
  'HAS_FIX_MARKER: ' + content.includes('LOCALIZATION_FIX'),
  'HAS_OLD_PATTERN: ' + content.includes('/api/localization/${'),
  '',
  'LINE32: ' + content.split('\n')[31]
].join('\n');

fs.writeFileSync(path.join(__dirname, '_check_result.txt'), result);
console.log(result);
