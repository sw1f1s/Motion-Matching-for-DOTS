# Motion Matching for DOTS — Samples

Необязательный пакет `com.collint.mmd.samples`: примеры интеграции MMD, игровое управление персонажем, камера, локомоция и действия, две сцены, benchmark, анимации, модели и тесты. Зависит от ядра `com.collint.mmd` версии 0.2.0.

## Установка

Требуются Unity 6000.3+ и URP. Сначала установите ядро `com.collint.mmd-0.2.0.tgz`, затем через **Install package from tarball** — `com.collint.mmd.samples-0.2.0.tgz`. Комплект `mmd-with-samples-0.2.0.zip` содержит оба архива: распакуйте его и установите их в том же порядке. Сохраняйте архивы по выбранным путям, поскольку проект ссылается на них. Для архивов Git LFS не требуется.

После публикации изменений в основную ветку можно добавить пакет через **Install package from Git URL**, предварительно установив ядро из той же ветки:

```text
https://github.com/sw1f1s/Motion-Matching-for-DOTS.git?path=Assets/Packages/com.collint.mmd.samples#main
```

Для Git-установки бинарных ресурсов нужен Git LFS. Samples объявляет зависимости на ядро, Input System, Animation и Terrain Physics. Остальные Unity-зависимости устанавливаются вместе с ядром. Samples не используется отдельно от MMD; его можно добавить позже или удалить, сохранив ядро.

После UPM-установки содержимое доступно в `Packages/com.collint.mmd.samples`, в проекте разработки — в `Assets/Packages/com.collint.mmd.samples`. Не импортируйте вторую копию в Assets.

## Примеры

- [MMDSampleEnvironment](Scenes/MMDSampleEnvironment.unity) — управляемый персонаж, бег, спринт, присед, слайд, прыжок и Traversal.
- [MMDSampleBenchmark](Scenes/MMDSampleBenchmark.unity) — несколько персонажей с общей базой анимаций и GPU-префабом.
- [MmdSampleWorld](Scripts/Lifecycle/MmdSampleWorld.cs) — создание мира и установка систем.
- [MmdSampleCharacterFactory](Scripts/Character/Helpers/MmdSampleCharacterFactory.cs) — инициализация персонажа.

Откройте нужную сцену. Для ввода установите **Active Input Handling** в **Input System Package (New)** или **Both**. В URP-проекте задайте pipeline asset в Graphics/Quality. Для изменения и повторного запекания установленных сцен и конфигураций сохраните редактируемые копии в Assets; кеш мировой физики запекается через **Bake World Physics**. Настройки bake по умолчанию сохраняют пользовательские базы в `Assets/MmdDatabases`.

Управление: WASD — движение, X — ходьба/бег, Shift — спринт, Ctrl — присед или слайд во время спринта, Space — прыжок или Traversal. Подробности архитектуры и отладки — в [README ядра](../com.collint.mmd/README.md).

## Тесты

Установите Unity Test Framework 1.6.0 и добавьте в `Packages/manifest.json` верхнеуровневое поле:

```json
"testables": ["com.collint.mmd.samples"]
```

Запускайте `Tests/EditMode` и `Tests/PlayMode` через Unity Test Runner. В проекте разработки Test Framework уже объявлен в manifest, а тесты компилируются из Assets. Тестовые ресурсы разрешаются через GUID, а результаты bake сохраняются в `Assets/MmdBakeTemporary/TestOutputs`, чтобы не писать в установленный пакет.

## Сборка архивов

В корне репозитория выполните `python3 Tools/package_release.py` для обоих пакетов и общего ZIP, либо `python3 Tools/package_release.py --package samples` только для Samples. Перед сборкой из нового clone загрузите бинарные ресурсы через `git lfs pull`; скрипт отклоняет незагруженные LFS pointers. Результаты находятся в `artifacts/packages` и не включаются в Git.

## Лицензия

Код — [MIT](LICENSE.md). Анимации, модели, текстуры и производные базы сохраняют исходные условия; [Third Party Notices](Third%20Party%20Notices.md) входит в этот пакет. Сборка локальных архивов не публикует их.
