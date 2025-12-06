const sharp = require('sharp');
const path = require('path');

const inputPath = path.join(__dirname, 'images', 'icon.svg');
const outputPath = path.join(__dirname, 'images', 'icon.png');

sharp(inputPath)
  .resize(128, 128)
  .png()
  .toFile(outputPath)
  .then(() => {
    console.log('✅ Icon converted successfully: icon.png (128x128)');
  })
  .catch(err => {
    console.error('❌ Error converting icon:', err);
  });
