# ForestSurvival

Кооперативное выживание в густом лесу: физика как в Spintires и My Summer Car, атмосфера хоррора. Unity 6000.6.0f1, Built-in RP, Netcode for GameObjects. Весь мир, модели, текстуры и звук создаются кодом при запуске.

Новая игра начинается с пролога: кордон №7, пустой бак и записка лесника. Управление и запуск на Mac описаны в [MAC_GUIDE.md](MAC_GUIDE.md), правила импорта ассетов в [ASSET_PIPELINE.md](ASSET_PIPELINE.md).

Сборка обеих платформ:

```bash
Unity.exe -quit -batchmode -projectPath . -executeMethod BuildScript.BuildAll
```
