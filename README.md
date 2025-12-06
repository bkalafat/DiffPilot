<div align="center">

# 🚀 DiffPilot

**PR kod incelemesi için akıllı MCP sunucusu**

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Protocol-00ADD8?style=for-the-badge&logo=json&logoColor=white)](https://modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)
[![GitHub](https://img.shields.io/badge/GitHub-bkalafat-181717?style=for-the-badge&logo=github)](https://github.com/bkalafat/DiffPilot)

<br/>

*AI destekli kod incelemesi, PR başlık ve açıklama oluşturma araçları*

[🎯 Özellikler](#-özellikler) •
[⚡ Kurulum](#-kurulum) •
[🔧 Kullanım](#-kullanım) •
[🛠️ Araçlar](#️-mcp-araçları) •
[📖 API](#-api-referansı)

</div>

---

## 📋 İçindekiler

- [Proje Hakkında](#-proje-hakkında)
- [Özellikler](#-özellikler)
- [Kurulum](#-kurulum)
- [Kullanım](#-kullanım)
- [MCP Araçları](#️-mcp-araçları)
- [API Referansı](#-api-referansı)
- [Proje Yapısı](#-proje-yapısı)
- [Katkıda Bulunma](#-katkıda-bulunma)
- [Lisans](#-lisans)

---

## 🎯 Proje Hakkında

**DiffPilot**, Model Context Protocol (MCP) üzerinden çalışan, PR (Pull Request) kod incelemesi için tasarlanmış bir sunucudur. JSON-RPC 2.0 protokolü kullanarak stdio üzerinden iletişim kurar ve AI destekli kod inceleme araçları sunar.

### 🤔 Neden DiffPilot?

- 🔍 **Otomatik Branch Algılama** - Hangi branch'ten ayrıldığınızı otomatik olarak tespit eder
- 📝 **PR Başlık Oluşturma** - Conventional commit formatında akıllı başlık önerileri
- 📄 **PR Açıklama Oluşturma** - Değişiklikleri özetleyen kapsamlı açıklamalar
- 🤖 **AI Kod İncelemesi** - Kod incelemesi için yapılandırılmış diff çıktısı
- ⚡ **Sıfır Bağımlılık** - Sadece .NET BCL kullanır, harici paket gerekmez

---

## ✨ Özellikler

| Özellik | Açıklama |
|---------|----------|
| 🔄 **Diff Alma** | İki branch arasındaki farkları alır |
| 📊 **Kod İnceleme** | AI destekli kod incelemesi için yapılandırılmış çıktı |
| 🏷️ **Başlık Oluşturma** | Conventional commit formatında PR başlığı |
| 📋 **Açıklama Oluşturma** | Checklist'li kapsamlı PR açıklaması |
| 🔍 **Branch Algılama** | Otomatik base/feature branch tespiti |
| ✅ **Git Doğrulama** | Güvenli komut yürütme |

---

## ⚡ Kurulum

### 📋 Gereksinimler

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) veya üzeri
- Git yüklü ve PATH'te erişilebilir

### 🔨 Derleme

```bash
# Projeyi klonlayın
git clone https://github.com/bkalafat/DiffPilot.git
cd DiffPilot

# Derleyin
dotnet build

# Çalıştırın
dotnet run
```

### 🔌 MCP Client Yapılandırması

DiffPilot'u bir MCP client (örn: Claude Desktop, VS Code Copilot) ile kullanmak için:

```json
{
  "mcpServers": {
    "diffpilot": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/DiffPilot"],
      "cwd": "/your/git/repository"
    }
  }
}
```

---

## 🔧 Kullanım

DiffPilot, MCP protokolü üzerinden stdin/stdout ile iletişim kurar. Aşağıda örnek kullanım senaryoları bulunmaktadır:

### 💡 Örnek Senaryo 1: PR Diff Alma

```
"main branch'e göre değişiklikleri göster"
```

### 💡 Örnek Senaryo 2: Kod İncelemesi

```
"Bu PR'daki değişiklikleri incele, güvenlik ve performans açısından değerlendir"
```

### 💡 Örnek Senaryo 3: PR Başlığı Oluşturma

```
"Bu değişiklikler için uygun bir PR başlığı öner"
```

### 💡 Örnek Senaryo 4: PR Açıklaması Oluşturma

```
"Bu PR için detaylı bir açıklama ve checklist oluştur"
```

---

## 🛠️ MCP Araçları

DiffPilot dört ana araç sunar:

### 1️⃣ `get_pr_diff`

İki branch arasındaki ham diff çıktısını alır.

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `baseBranch` | string | ❌ | Hedef branch (varsayılan: otomatik algıla) |
| `featureBranch` | string | ❌ | Kaynak branch (varsayılan: mevcut branch) |
| `remote` | string | ❌ | Git remote adı (varsayılan: origin) |

---

### 2️⃣ `review_pr_changes`

Kod incelemesi için diff ile birlikte AI talimatları sağlar.

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `baseBranch` | string | ❌ | Hedef branch |
| `focusAreas` | string | ❌ | Odaklanılacak alanlar (ör: "güvenlik, performans") |

---

### 3️⃣ `generate_pr_title`

Değişikliklerden conventional commit formatında başlık oluşturur.

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `baseBranch` | string | ❌ | Hedef branch |
| `style` | string | ❌ | Başlık stili: `conventional`, `descriptive`, `ticket` |

**Çıktı Örnekleri:**
- `feat: add user authentication`
- `fix: resolve memory leak in data processor`
- `refactor: simplify API response handling`

---

### 4️⃣ `generate_pr_description`

Kapsamlı PR açıklaması oluşturur.

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `baseBranch` | string | ❌ | Hedef branch |
| `ticketUrl` | string | ❌ | İlişkili ticket/issue URL'i |
| `includeChecklist` | boolean | ❌ | PR checklist'i dahil et (varsayılan: true) |

---

## 📖 API Referansı

DiffPilot, JSON-RPC 2.0 protokolünü kullanır. Desteklenen metodlar:

### `initialize`
Sunucu yeteneklerini ve sürüm bilgisini döndürür.

### `tools/list`
Mevcut araçların listesini ve şemalarını döndürür.

### `tools/call`
Belirtilen aracı çalıştırır ve sonucu döndürür.

### 📨 Örnek İstek

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "get_pr_diff",
    "arguments": {
      "baseBranch": "main"
    }
  }
}
```

### 📩 Örnek Yanıt

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "## Diff: origin/main → feature/my-feature\n\n```diff\n..."
      }
    ],
    "isError": false
  }
}
```

---

## 📁 Proje Yapısı

```
DiffPilot/
├── 📄 DiffPilot.csproj          # Proje dosyası
├── 📄 DiffPilot.sln             # Solution dosyası
├── 📄 README.md                  # Bu dosya
└── 📂 src/
    ├── 📄 Program.cs            # Giriş noktası - JSON-RPC döngüsü
    ├── 📂 Protocol/
    │   ├── 📄 JsonRpcModels.cs  # İstek/Yanıt modelleri
    │   └── 📄 McpHandlers.cs    # MCP metod işleyicileri
    ├── 📂 Git/
    │   └── 📄 GitService.cs     # Git komut yürütme
    └── 📂 Tools/
        ├── 📄 ToolResult.cs     # Araç sonuç wrapper'ı
        └── 📄 PrReviewTools.cs  # Araç implementasyonları
```

---

## 🏗️ Teknik Detaylar

### 📡 İletişim Protokolü

- **Transport:** stdio (stdin/stdout)
- **Protokol:** JSON-RPC 2.0
- **Encoding:** UTF-8, satır sonu ile ayrılmış mesajlar

### ⚠️ Önemli Kurallar

- ✅ Stdout'a sadece geçerli JSON-RPC yanıtları yazılır
- ✅ Log/debug çıktıları stderr'e yönlendirilir
- ✅ Notification'lar (id olmayan istekler) yanıt almaz
- ✅ Hiçbir dosya oluşturulmaz - tüm çıktılar doğrudan döndürülür

---

## 🤝 Katkıda Bulunma

Katkılarınızı bekliyoruz! 🎉

1. 🍴 Projeyi fork edin
2. 🌿 Feature branch oluşturun (`git checkout -b feature/harika-ozellik`)
3. 💾 Değişikliklerinizi commit edin (`git commit -m 'feat: harika özellik eklendi'`)
4. 📤 Branch'i push edin (`git push origin feature/harika-ozellik`)
5. 🔃 Pull Request açın

### 📝 Commit Mesajı Formatı

[Conventional Commits](https://www.conventionalcommits.org/) formatını kullanıyoruz:

- `feat:` - Yeni özellik
- `fix:` - Hata düzeltme
- `docs:` - Dokümantasyon
- `refactor:` - Kod yeniden düzenleme
- `test:` - Test ekleme/düzeltme
- `chore:` - Bakım işleri

---

## 📄 Lisans

Bu proje MIT lisansı altında dağıtılmaktadır. Detaylar için [LICENSE](LICENSE) dosyasına bakın.

---

## 👤 İletişim

**Geliştirici:** [@bkalafat](https://github.com/bkalafat)

**Proje Linki:** [https://github.com/bkalafat/DiffPilot](https://github.com/bkalafat/DiffPilot)

---

<div align="center">

### ⭐ Beğendiyseniz yıldız vermeyi unutmayın!

DiffPilot ile 💜 yapıldı

</div>
