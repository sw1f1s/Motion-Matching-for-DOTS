# Motion Matching for DOTS

MMD (`com.collint.mmd`, namespace `ColInt.MMD`) — пакет скелетной анимации для Unity Entities. Он подбирает анимацию по траектории и позе персонажа, сглаживает переходы, применяет root motion в ECS и поддерживает GPU-деформацию через Entities Graphics.

## Установка

Требуются Unity 6000.3+, URP, Git и Git LFS. В Package Manager выберите **Install package from Git URL**:

```text
https://github.com/sw1f1s/Motion-Matching-for-DOTS.git?path=Assets/Packages/com.collint.mmd#v0.1.0
```

Для версии из основной ветки замените `#v0.1.0` на `#main`. Зависимости объявлены в `package.json`; примеры и тесты входят в пакет.

## Состав пакета

- `Runtime` — поиск анимаций, playback, root motion, Physics и Graphics.
- `Editor` — запекание баз и Debugger.
- `Samples` — примеры интеграции, сцена персонажа, benchmark, ресурсы и тесты.

Исходники находятся в `Assets/Packages/com.collint.mmd`. Описание компонентов, подготовки анимаций и подключения к своему ECS-миру — в [README пакета](Assets/Packages/com.collint.mmd/README.md).

## Лицензия

Код — [MIT](LICENSE). Условия сторонних анимаций и моделей указаны в [Third Party Notices](Assets/Packages/com.collint.mmd/Third%20Party%20Notices.md).
