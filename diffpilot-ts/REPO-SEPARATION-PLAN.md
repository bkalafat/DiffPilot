# DiffPilot TypeScript - Repo Separation Plan

Bu belge, `diffpilot-ts` projesini ana DiffPilot reposundan ayırıp bağımsız bir repo haline getirme planını içerir.

## 📋 Mevcut Durum

```
DiffPilot/                     # Ana C# repo
├── src/                       # C# MCP Server
├── vscode-extension/          # C# tabanlı VS Code Extension
└── diffpilot-ts/              # ✨ TypeScript MCP Server (ayırılacak)
    ├── src/                   # TS MCP Server kaynak kodu
    ├── tests/                 # Vitest testleri
    └── vscode-extension/      # ✨ TS tabanlı VS Code Extension
```

## 🎯 Hedef Yapı

### Yeni Repo: `DiffPilot-TS`

```
DiffPilot-TS/
├── src/                       # MCP Server TypeScript kodu
│   ├── index.ts
│   ├── git/
│   ├── security/
│   ├── tools/
│   └── utils/
├── tests/                     # Vitest testleri
├── vscode-extension/          # VS Code Extension
│   ├── src/
│   ├── images/
│   ├── server/                # Bundled MCP server (build artifact)
│   └── package.json
├── package.json
├── tsconfig.json
├── vitest.config.ts
├── README.md
├── LICENSE
├── SECURITY.md
└── .github/
    ├── workflows/
    │   ├── ci.yml
    │   ├── release.yml
    │   └── publish-extension.yml
    └── copilot-instructions.md
```

---

## 🚀 Adım Adım Ayırma Planı

### Adım 1: Yeni GitHub Repo Oluştur

```bash
# GitHub'da yeni repo oluştur: DiffPilot-TS
# - Public repo
# - MIT License
# - Add README
```

### Adım 2: Dosyaları Kopyala (Git History'siz)

```bash
# Yeni bir klasör oluştur
mkdir DiffPilot-TS
cd DiffPilot-TS

# Git başlat
git init
git branch -M main

# diffpilot-ts içeriğini kopyala
cp -r ../DiffPilot/diffpilot-ts/* .
cp -r ../DiffPilot/diffpilot-ts/.* . 2>/dev/null || true

# Gereksiz dosyaları sil
rm -rf node_modules dist .git
rm -rf vscode-extension/node_modules vscode-extension/out vscode-extension/server

# İlk commit
git add .
git commit -m "Initial commit: DiffPilot TypeScript MCP Server"

# Remote ekle ve push
git remote add origin https://github.com/bkalafat/DiffPilot-TS.git
git push -u origin main
```

### Adım 3: Git History ile Taşıma (Opsiyonel - Tercih edilirse)

```bash
# git-filter-repo kullanarak sadece diffpilot-ts klasörünü al
cd DiffPilot
git filter-repo --path diffpilot-ts/ --path-rename diffpilot-ts/:

# Bu işlem repo'yu değiştirir, dikkatli kullan!
```

### Adım 4: CI/CD Workflow'ları Oluştur

#### `.github/workflows/ci.yml`
```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
      
      - name: Install dependencies
        run: npm ci
      
      - name: Run tests
        run: npm test
      
      - name: Build
        run: npm run build
```

#### `.github/workflows/publish-extension.yml`
```yaml
name: Publish Extension

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
      
      - name: Install dependencies
        run: |
          npm ci
          cd vscode-extension && npm ci
      
      - name: Build and Package
        run: |
          cd vscode-extension
          npm run build:server
          npx vsce package
      
      - name: Publish to Marketplace
        run: |
          cd vscode-extension
          npx vsce publish
        env:
          VSCE_PAT: ${{ secrets.VSCE_PAT }}
```

### Adım 5: npm Paket Yayını (Opsiyonel)

```bash
# package.json'a ekle
{
  "name": "@diffpilot/mcp-server",
  "publishConfig": {
    "access": "public"
  }
}

# npm'e yayınla
npm login
npm publish --access public
```

### Adım 6: Ana README'yi Güncelle

```markdown
# DiffPilot TypeScript

🚀 MCP Server for GitHub Copilot, Claude, and AI assistants

## Installation

### VS Code Extension
Install from [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=BurakKalafat.diffpilot)

### npm (for custom integration)
```bash
npm install @diffpilot/mcp-server
```

### npx (direct usage)
```bash
npx diffpilot
```
```

---

## 📦 VS Code Extension Yayın Stratejisi

### Extension ID Seçenekleri

1. **Aynı ID kullan** (Recommended)
   - ID: `BurakKalafat.diffpilot`
   - C# versiyonu deprecate et, TS versiyonunu aynı extension olarak yayınla
   - Kullanıcılar otomatik güncellenir

2. **Yeni ID kullan**
   - ID: `BurakKalafat.diffpilot-ts`
   - Her iki versiyon da markette kalır
   - Kullanıcılar seçim yapar

### Önerilen Yaklaşım: Aynı ID

```json
// vscode-extension/package.json
{
  "name": "diffpilot",
  "publisher": "BurakKalafat",
  "version": "2.0.0"  // Major version bump
}
```

**Changelog'da belirt:**
```markdown
## [2.0.0] - 2025-12-10
### ⚠️ BREAKING CHANGE
- Complete rewrite in TypeScript
- **No longer requires .NET 9 SDK**
- Same functionality, better performance
```

---

## ✅ Kontrol Listesi

### Repo Oluşturma
- [ ] GitHub'da `DiffPilot-TS` repo oluştur
- [ ] README.md güncelle
- [ ] LICENSE dosyası ekle
- [ ] SECURITY.md ekle

### Kod Taşıma
- [ ] diffpilot-ts klasörünü kopyala
- [ ] node_modules ve build artifact'ları temizle
- [ ] İlk commit yap
- [ ] Push to origin

### CI/CD
- [ ] GitHub Actions workflow'ları oluştur
- [ ] VSCE_PAT secret ekle
- [ ] NPM_TOKEN secret ekle (npm yayını için)

### Yayın
- [ ] VS Code extension yayınla (v2.0.0)
- [ ] npm paket yayınla (opsiyonel)
- [ ] Release notes yaz
- [ ] C# repo README'sine TypeScript alternatifini ekle

### Dokümantasyon
- [ ] Installation guide güncelle
- [ ] Migration guide yaz (C# → TS)
- [ ] API documentation oluştur

---

## 🔄 C# Repo ile İlişki

Ana DiffPilot (C#) repo'sunda:

```markdown
## Alternative: TypeScript Version

For environments without .NET SDK, use the [TypeScript version](https://github.com/bkalafat/DiffPilot-TS):

- No .NET required
- Same 9 MCP tools
- Smaller footprint
```

---

## ⏱️ Tahmini Süre

| Adım | Süre |
|------|------|
| Repo oluşturma | 5 dk |
| Dosya kopyalama | 10 dk |
| CI/CD kurulumu | 30 dk |
| Extension yayını | 15 dk |
| Dokümantasyon | 30 dk |
| **Toplam** | **~1.5 saat** |

---

## 📝 Notlar

1. **Git History**: Clean start önerilir (tarihsiz). Çünkü:
   - C# kodu ile karışık tarih anlamsız
   - Daha küçük repo
   - Temiz başlangıç

2. **Extension Versiyonu**: v2.0.0 major bump
   - Kullanıcıları uyarır
   - Breaking change belli eder

3. **npm Paketi**: Opsiyonel ama faydalı
   - MCP registry'de listeleme kolaylaşır
   - `npx diffpilot` ile kullanım

4. **Dual Maintenance**: Gerekli değil
   - TS versiyonu primary olabilir
   - C# versiyonu archived/maintenance mode

---

*Bu plan hazırlandı: 2025-12-10*
