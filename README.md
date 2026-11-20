# Plataforma Launcher

Aplicativo desktop multiplataforma em Avalonia para autenticação e acesso futuro à biblioteca da plataforma.

A logo temporária está em `src/Plataforma.Launcher/Assets/logo_teste.svg`; `logo_teste.png` é usada na interface e `logo_teste.ico` na janela e no executável Windows. Ao receber a logo oficial, substitua o SVG e regenere os formatos PNG/ICO antes de publicar uma nova build.

## Requisitos

- .NET 10 SDK
- Velopack CLI 1.2.161 para gerar instaladores localmente

## Executar

Na raiz do repositório:

```powershell
dotnet run --project .\src\Plataforma.Launcher\Plataforma.Launcher.csproj
```

No modo Debug, o launcher usa `http://localhost:5000` como endereço padrão da Auth API. Em builds Release, usa `https://plataforma.crefeed.com.br/auth-api/`. Para apontar para outro endereço, configure a variável de ambiente `PLATAFORMA_API_BASE_URL` com a URL base HTTP ou HTTPS absoluta, sem query nem fragmento. HTTP é permitido apenas para loopback; APIs remotas devem usar HTTPS.

## Instalador Windows local

Instale o Velopack CLI na versão usada pelo launcher e gere um instalador self-contained para Windows x64 com:

```powershell
dotnet tool install --global vpk --version 1.2.161
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-windows-installer.ps1
```

O arquivo `artifacts/windows/release/Plataforma_laucher.exe` instala por usuário e registra o protocolo `playlab://`. O primeiro instalador Velopack substitui a instalação antiga do Inno Setup; instalações antigas precisam ser removidas e instaladas novamente uma única vez para habilitar as atualizações automáticas. A build ainda não tem assinatura de código e o Windows pode exibir um aviso do SmartScreen.

## Releases multiplataforma e atualizações

O workflow `.github/workflows/release-launcher.yml` publica automaticamente após cada push em `main` e também pode ser iniciado manualmente pelo GitHub Actions. Ele gera Windows x64, macOS Intel, macOS Apple Silicon e Linux x64; usa `1.<número da execução>.<tentativa>` como versão para permitir repetir releases com segurança. O launcher consulta o canal do sistema ao abrir, baixa atualizações (deltas quando disponíveis) em segundo plano e reinicia apenas o próprio launcher para aplicar a versão.

Configure estas variáveis do repositório em **Settings → Secrets and variables → Actions → Variables**:

- `BUNNY_STORAGE_ZONE_NAME`: nome do Storage Zone de publicação.
- `BUNNY_STORAGE_API_ENDPOINT`: endpoint HTTP da região do Storage Zone, por exemplo `https://storage.bunnycdn.com`.
- `BUNNY_CDN_BASE_URL`: URL HTTPS da Pull Zone usada pela Auth API.

Configure estes secrets no mesmo painel:

- `BUNNY_STORAGE_ACCESS_KEY`: senha/API key do Storage Zone, usada somente para upload.
- `BUNNY_API_KEY`: API key da conta Bunny, usada para invalidar o cache dos instaladores com nome fixo e dos feeds.

O workflow publica `Plataforma_laucher.exe`, `Plataforma_laucher_macos_x64.pkg`, `Plataforma_laucher_macos_arm64.pkg` e `Plataforma_laucher_linux_x64.AppImage` diretamente em `Plataform-Build/`. Os feeds e pacotes de atualização ficam em `Plataform-Build/updates/{canal}`. Antes do primeiro release, atualize a Auth API e configure esses mesmos caminhos no `.env` de produção. Builds macOS sem assinatura/notarização são apenas para testes e podem exigir liberação manual pelo sistema.

Os tokens de acesso e de refresh são mantidos somente em memória. O launcher pode guardar o e-mail após um login bem-sucedido quando “Lembrar meu e-mail” estiver marcado; a senha nunca é salva. Fechar o aplicativo encerra a sessão local.

O fluxo de login integra `POST /api/auth/login`, consulta `GET /api/auth/me` e encerra a sessão com `POST /api/auth/logout`. “Esqueceu a senha?” solicita um link por `POST /api/auth/forgot-password`; o link abre a página segura de redefinição no portal. Após entrar, o launcher carrega a biblioteca persistente por `GET /api/library`. Para Dash & Crash, o modo sandbox do backend pode conceder o entitlement a todas as contas de teste; o launcher nunca simula essa licença localmente.

O launcher solicita acesso ao build Windows em `GET /api/games/{slug}/access?platform=windows`, baixa o ZIP com link temporário da Bunny CDN, extrai-o para `%LOCALAPPDATA%\Playlab\Games` e permite iniciar o jogo instalado. A extração usa as proteções do .NET contra caminhos de arquivo fora da pasta de destino. Se o ZIP tiver vários executáveis, escolha o executável principal na biblioteca. O botão WebGL continua abrindo o link temporário no navegador padrão. Os builds de jogos conectados ainda são Windows; no macOS e Linux o launcher pode autenticar e atualizar, mas a execução de builds nativos do jogo será publicada em outra etapa.

No Windows, a versão publicada registra o protocolo `playlab://game/{slug}` para o usuário atual. O botão “Jogar no PC” do portal abre o launcher; depois do login, ele inicia o download da build se o jogo ainda não estiver instalado. Se já estiver instalado, o launcher o inicia. O download não inicia o jogo automaticamente após a instalação: a pessoa pode revisar a instalação e então clicar em “Jogar”. O launcher também encaminha links para a instância já aberta.

O portal web baixa separadamente instaladores por `GET /api/launcher/{platform}/access`, sem exigir login. A API entrega somente uma URL Bunny temporária para cada arquivo; os builds de jogos continuam exigindo autenticação e licença. Configure os caminhos dos instaladores e feeds no backend conforme a documentação do repositório da plataforma. O launcher e o portal não recebem credenciais do Storage Zone nem a chave HMAC.
