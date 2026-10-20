# Motion Matching for DOTS — Samples

Необязательный пакет `com.collint.mmd.samples`: примеры интеграции MMD, игровое управление персонажем, камера, локомоция и действия, две сцены, benchmark, анимации, модели и тесты. Зависит от ядра `com.collint.mmd` версии 0.3.2.

## Установка

Требуются Unity 6000.3+ и URP. Сначала установите ядро `com.collint.mmd-0.3.2.tgz`, затем через **Install package from tarball** — `com.collint.mmd.samples-0.3.2.tgz`. Комплект `mmd-with-samples-0.3.2.zip` содержит оба архива: распакуйте его и установите их в том же порядке. Сохраняйте архивы по выбранным путям, поскольку проект ссылается на них. Для архивов Git LFS не требуется.

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

## Слои анимации

В `Configurations/AnimationMasks` находятся маски RightArm, LeftArm и UpperBody для SampleCharacter. `Configurations/SampleAnimationLayers.asset` включает слой Hammer Right Arm: правая рука и пальцы используют зацикленный Run_Combat_Loop_F_0 из группы Hammer Run (Layer Only). Клип выбирается через Motion Matching по движению персонажа. Слой ограничен явным списком 33 существующих базовых групп; новые группы, например удары, не включают его автоматически. Список меняется через поле флагов Base Groups (Everything / Nothing / отдельные группы). Левая рука, корпус, ноги и root motion остаются от базовой анимации. Назначьте его в **Sample Character Configuration → Animation Layers** или создайте собственный asset через **Create → MMD → Animation Layers**. Для оружия добавьте свой клип в bake-группу с **Playback → Layer Only**, запеките базу и выберите эту группу в слое. Подробности настройки весов и runtime API — в [README ядра](../com.collint.mmd/README.md#слои-и-маски-анимации).

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
