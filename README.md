# ForestSurvival

Кооперативное выживание в густом лесу: физика как в Spintires и My Summer Car, атмосфера хоррора. Unity 6000.6.0f1, Built-in RP, Netcode for GameObjects. Весь мир, модели, текстуры и звук создаются кодом при запуске.

Новая игра начинается с пролога: кордон №7, пустой бак и записка лесника. Управление и запуск на Mac описаны в [docs/MAC_GUIDE.md](docs/MAC_GUIDE.md), правила импорта ассетов в [docs/ASSET_PIPELINE.md](docs/ASSET_PIPELINE.md).

Сборка обеих платформ и архив для Mac:

```bash
Unity.exe -quit -batchmode -projectPath . -executeMethod BuildScript.BuildAll
py -3 Tools/zip_mac.py
```

## Где что лежит

| Папка | Что в ней |
| --- | --- |
| `Assets/Scripts/Audio` | звук |
| `Assets/Scripts/Characters` | люди и то, что ходит в лесу |
| `Assets/Scripts/Diagnostics` | отчёты об ошибках |
| `Assets/Scripts/Game` | правила игры и сохранения, `Menu` меню, `Story` сюжет |
| `Assets/Scripts/Interaction` | прицел и подсказки |
| `Assets/Scripts/Items` | предметы |
| `Assets/Scripts/Network` | кооператив |
| `Assets/Scripts/Player` | игрок |
| `Assets/Scripts/Vehicle` | машина, `Cabin` кабина и кузов, `Upgrades` детали, `Wrecks` брошенные машины |
| `Assets/Scripts/World` | `Generation` создание мира, `Environment` погода и свет, `Locations` места |
| `Assets/Resources` | шейдеры |
| `docs` | инструкции |
| `Tools` | вспомогательные скрипты |
| `Build/Windows` | игра для Windows, в git не хранится |
| `Build/macOS` | игра для Mac и архив `ForestSurvival_macOS.zip`, в git не хранятся |
