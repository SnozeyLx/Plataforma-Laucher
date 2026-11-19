# Plataforma Launcher

Aplicativo desktop multiplataforma em Avalonia para autenticação e acesso futuro à biblioteca da plataforma.

A logo temporária está em `src/Plataforma.Launcher/Assets/logo_teste.svg`; `logo_teste.png` é usada na interface e `logo_teste.ico` na janela e no executável Windows. Ao receber a logo oficial, substitua o SVG e regenere os formatos PNG/ICO antes de publicar uma nova build.

## Requisitos

- .NET 10 SDK
- Inno Setup 6 para gerar o instalador Windows

## Executar

Na raiz do repositório:

```powershell
dotnet run --project .\src\Plataforma.Launcher\Plataforma.Launcher.csproj
```

O launcher usa `http://localhost:5000` como endereço padrão da Auth API. Para apontar para outro endereço, configure a variável de ambiente `PLATAFORMA_API_BASE_URL` com uma URL HTTP ou HTTPS absoluta, sem query nem fragmento. HTTP é permitido apenas para loopback; APIs remotas devem usar HTTPS.

## Instalador Windows

Gere o instalador self-contained para Windows x64 com:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-windows-installer.ps1
```

O arquivo `artifacts/windows/installer/Plataforma-Launcher-Setup-win-x64.exe` instala por usuário, cria um atalho no menu Iniciar, oferece um atalho opcional na área de trabalho e registra o protocolo `playlab://`. A instalação e a desinstalação não exigem privilégios de administrador. O ZIP portátil continua disponível como alternativa. A build atual ainda não tem assinatura de código; o Windows pode exibir um aviso do SmartScreen até que seja usado um certificado de assinatura confiável.

Os tokens de acesso e de refresh são mantidos somente em memória nesta etapa. Fechar o aplicativo encerra a sessão local; armazenamento persistente de credenciais será definido em uma etapa posterior.

O fluxo de login integra `POST /api/auth/login`, consulta `GET /api/auth/me` e encerra a sessão com `POST /api/auth/logout`. Após entrar, o launcher carrega a biblioteca persistente por `GET /api/library`. Para Dash & Crash, o modo sandbox do backend pode conceder o entitlement a todas as contas de teste; o launcher nunca simula essa licença localmente.

O launcher solicita acesso ao Windows em `GET /api/games/{slug}/access?platform=windows`, baixa o ZIP com link temporário da Bunny CDN, extrai-o para `%LOCALAPPDATA%\Playlab\Games` e permite iniciar o jogo instalado. A extração usa as proteções do .NET contra caminhos de arquivo fora da pasta de destino. Se o ZIP tiver vários executáveis, escolha o executável principal na biblioteca. O botão WebGL continua abrindo o link temporário no navegador padrão. As ações permanecem indisponíveis enquanto a API não confirmar a licença e a configuração do build.

No Windows, a versão publicada registra o protocolo `playlab://game/{slug}` para o usuário atual. O botão “Jogar no PC” do portal abre o launcher; depois do login, ele inicia o download da build se o jogo ainda não estiver instalado. Se já estiver instalado, o launcher o inicia. O download não inicia o jogo automaticamente após a instalação: a pessoa pode revisar a instalação e então clicar em “Jogar”. O launcher também encaminha links para a instância já aberta.

O portal web baixa separadamente o instalador do launcher por `GET /api/launcher/windows/access`, sem exigir login. A API entrega somente uma URL Bunny temporária para esse arquivo; os builds de jogos continuam exigindo autenticação e licença. Configure `PLATFORM_LAUNCHER_WINDOWS_OBJECT_PATH` no backend com a chave completa do instalador. O launcher e o portal não recebem credenciais do Storage Zone nem a chave HMAC. Configure o modo sandbox, a Pull Zone e os caminhos dos builds somente no backend, conforme a documentação do repositório da plataforma.
