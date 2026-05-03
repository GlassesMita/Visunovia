const path = require('path');
const fs = require('fs');

const NWJS_SDK_VERSION = process.env.NWJS_SDK_VERSION || '0.90.0';
const PLATFORM = process.env.PLATFORM || 'win64';
const OUTPUT_DIR = path.join(__dirname, '..', 'dist');

function replacePlaceholder(filePath, placeholder, value) {
    if (fs.existsSync(filePath)) {
        let content = fs.readFileSync(filePath, 'utf8');
        content = content.replace(new RegExp(placeholder, 'g'), value);
        fs.writeFileSync(filePath, content, 'utf8');
        console.log(`Replaced ${placeholder} in ${filePath}`);
    }
}

async function build() {
    console.log('Starting NW.js build...');
    console.log(`Platform: ${PLATFORM}`);
    console.log(`NW.js SDK Version: ${NWJS_SDK_VERSION}`);

    const projectDir = __dirname;
    const pkgJsonPath = path.join(projectDir, 'package.json');
    const cryptoJsPath = path.join(projectDir, 'Security', 'simple-crypto.js');

    if (!fs.existsSync(pkgJsonPath)) {
        console.error('package.json not found!');
        return;
    }

    console.log('\n[Step 1] Install dependencies...');
    require('child_process').execSync('npm install', {
        cwd: projectDir,
        stdio: 'inherit'
    });

    console.log('\n[Step 2] Build with nw-builder...');
    const nwBuilder = require('nw-builder');
    const nw = new nwBuilder({
        version: NWJS_SDK_VERSION,
        platforms: [PLATFORM],
        buildDir: OUTPUT_DIR,
        appDir: projectDir,
        force: true
    });

    nw.on('log', console.log);
    nw.on('error', console.error);

    await nw.build();

    console.log('\nBuild complete!');
    console.log(`Output: ${OUTPUT_DIR}`);
}

build().catch(err => {
    console.error('Build failed:', err);
    process.exit(1);
});
