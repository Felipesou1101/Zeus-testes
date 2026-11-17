# Zeus Fiscal Monitor

MVP desktop (WPF) para **consulta de status da SEFAZ**, **validação XSD** e **assinatura digital** de NF-e/NFC-e,
com **trilha de auditoria** em PostgreSQL. Construído com Clean Architecture, DDD e SOLID.

## Stack

| Camada | Tecnologia |
|---|---|
| UI | WPF · MahApps.Metro 2.4 · CommunityToolkit.Mvvm 8.4 (`[ObservableProperty]`, `[RelayCommand]`, `IMessenger`) |
| DI / Config | Microsoft.Extensions.DependencyInjection · Configuration.Json · Http (typed `HttpClient`) |
| Persistência | PostgreSQL 15 (Docker) · EF Core + Npgsql · trigger PL/pgSQL de auditoria (JSONB) |
| Fiscal | `SignedXml` + `X509Certificate2` (XMLDSig RSA-SHA1) · `XmlReaderSettings` + XSD · SOAP 1.2 `NfeStatusServico4` |
| Testes | xUnit v3 · Moq · AAA / F.I.R.S.T. (86 testes, sem rede, sem banco) |

> **Target framework:** .NET 10 (`net10.0` / `net10.0-windows`) — é o SDK/runtime instalado nesta máquina.
> Para .NET 8, troque `TargetFramework` nos 4 `.csproj` e os pacotes `10.0.x` por `8.0.x`.

## Arquitetura

```
src/
├── ZeusFiscalMonitor.Domain          ← núcleo, sem dependências externas
│   ├── ValueObjects/   Uf (27 UFs + código IBGE), ChaveAcesso (44 dígitos + DV módulo 11)
│   ├── Entities/       DocumentoFiscal (máquina de estados), TimelineEventoFiscal
│   ├── DTOs/           records imutáveis
│   ├── Interfaces/     IValidadorDocumento (Strategy), IAssinadorXmlService, ISefazSoapService, IFiscalRepository
│   └── Services/       ProcessadorDocumentoFiscal (orquestra validar → assinar → auditar)
├── ZeusFiscalMonitor.Infraestrutura  ← implementa as interfaces do domínio (DIP)
│   ├── Data/           AppDbContext (HasConversion para os Value Objects)
│   ├── Repositories/   FiscalRepository (IDbContextFactory: contexto curto por operação)
│   ├── Validadores/    ValidadorNFe / ValidadorNFCe (+ XSD embutido como recurso)
│   ├── Servicos/       AssinadorXmlService, SefazSoapService, CertificadoTesteFactory
│   └── DependencyInjection.cs  (AddInfraestrutura)
├── ZeusFiscalMonitor.Desktop         ← MVVM; code-behind vazio; Composition Root em App.xaml.cs
└── ZeusFiscalMonitor.Testes
```

Fluxo de estados do `DocumentoFiscal`: `PENDENTE → VALIDADO → ASSINADO`, qualquer etapa pode ir para `ERRO`;
reenviar um XML com a mesma chave reinicia o ciclo. Transições inválidas lançam `DomainException`.

### Pontos de SOLID para conversar

- **SRP** – validação, assinatura, comunicação SOAP e persistência são classes distintas.
- **OCP + Strategy** – `ProcessadorDocumentoFiscal` resolve o validador por dicionário (`TipoDocumento → IValidadorDocumento`).
  Suportar CT-e = criar `ValidadorCTe` e registrar uma linha no DI; nenhum `if/else` muda.
- **LSP** – `ValidadorNFe`/`ValidadorNFCe` são intercambiáveis (Template Method na base XSD).
- **ISP** – quatro interfaces pequenas, cada consumidor depende só do que usa.
- **DIP** – o domínio define as interfaces; a infraestrutura as implementa; a UI só conhece abstrações.

### Auditoria em duas origens

A tabela `timeline_eventos_fiscais` recebe eventos de:
- `APLICACAO_CSHARP` – etapas de negócio (`VALIDACAO_XSD`, `ASSINATURA`) com o diff `{"status_validacao": [antes, depois]}`;
- `BANCO_TRIGGER` – a trigger `trg_auditar_notas_fiscais` registra qualquer INSERT/UPDATE/DELETE, inclusive alterações manuais fora da aplicação.

## Como rodar

```powershell
# 1. Banco (os scripts de banco/ rodam automaticamente na 1ª subida do volume)
copy .env_exemplo .env
docker compose up -d

# 2. Testes
cd src
dotnet test --project ZeusFiscalMonitor.Testes

# 3. Aplicação
dotnet run --project ZeusFiscalMonitor.Desktop
```

Credenciais padrão: `zeus / zeus_dev_123`, banco `zeus_fiscal`, porta `5432`
(em sincronia com `src/ZeusFiscalMonitor.Desktop/appsettings.json`).

> Alterou os scripts SQL depois da primeira subida? Recrie o volume: `docker compose down -v && docker compose up -d`.

### Roteiro de demonstração

1. **Validador & Assinador** → *Abrir XML…* → `exemplos/nfe_exemplo.xml` → *Validar XSD* (status `VALIDADO`).
2. *Gerar teste* (cria um `.pfx` autoassinado em `Documentos\ZeusFiscalMonitor`, senha `zeus123`) → *Validar e Assinar*
   → o XML assinado aparece à direita com `<Signature>` após `<infNFe>`.
3. Troque o tipo para **NFCE** com o XML de NF-e → erro de regra (`<mod>65</mod>`). Apague uma tag obrigatória → erro de schema com linha/coluna.
4. **Histórico** → eventos das duas origens; selecione uma linha para ver o diff JSONB.
5. **Consulta SEFAZ** → UF `SE` → *Consultar* (ver nota sobre certificado abaixo).

### Consulta SEFAZ e certificado A1

A SEFAZ exige **TLS mútuo com e-CNPJ A1 ICP-Brasil**, mesmo para consultar status. Sem certificado a aba mostra o erro
de comunicação de forma tratada. Com um certificado real, configure em `appsettings.Local.json`
(ignorado pelo git, sobrescreve o `appsettings.json`):

```json
{ "Sefaz": { "CaminhoCertificado": "C:\\certs\\empresa.pfx", "SenhaCertificado": "..." } }
```

Retorno esperado em operação normal: `cStat 107 – Serviço em Operação`. As URLs de homologação por autorizador ficam em
`SefazEndpointsHomologacao` (SE é atendida pela SVRS) — confira periodicamente no Portal Nacional da NF-e.

## Limitações conhecidas (MVP)

- O XSD em `Infraestrutura/Schemas` é **simplificado** (estrutura principal e tipos-chave). Em produção, use o pacote oficial
  `PL_009_V4` (`nfe_v4.00.xsd` + includes) com um `XmlResolver` para os `xs:include`.
- O certificado gerado por *Gerar teste* serve apenas para demonstrar a assinatura; a SEFAZ não o aceita.
- Não há envio de lote/autorização (`NFeAutorizacao4`) — fora do escopo do MVP.
