# Motion Matching for DOTS

MMD — плагин анимации для Unity Entities: Motion Matching, воспроизведение скелетной анимации, root motion и GPU-деформация через Entities Graphics. Идентификатор: `com.collint.mmd`, namespace: `ColInt.MMD`.

## Установка

Unity 6000.3+ и URP. Для бинарных Sample-ресурсов нужен установленный Git LFS. В Package Manager выберите **Install package from Git URL**:

```text
https://github.com/sw1f1s/Motion-Matching-for-DOTS.git?path=Assets/Packages/com.collint.mmd#v0.1.0
```

Ссылка закрепляет версию `v0.1.0`. Для обновлений из основной ветки замените `#v0.1.0` на `#main`.
Entities 1.4.3, Entities Graphics 1.4.3, Unity Physics 1.4.3, Collections,
Burst, Mathematics, Newtonsoft JSON, Input System и URP указаны в `package.json` и разрешаются UPM.

`Samples` входит в пакет и содержит примеры интеграции, сцены, анимации и тесты. После установки он доступен в `Packages/com.collint.mmd/Samples`; в проекте разработки — в `Assets/Packages/com.collint.mmd/Samples`. Для тестов нужен Unity Test Framework.

Код распространяется под MIT; сторонние анимации и модели сохраняют исходные условия.
См. [Third Party Notices](Third%20Party%20Notices.md).

## Как работает

При запекании клипы превращаются в `.bytes` базу: позы костей, движение корня, признаки для поиска и именованные группы. Постоянные каналы сохраняются одним значением, вращения костей упаковываются с ограничением погрешности.

Каждый фиксированный ECS-такт игровая система задаёт желаемую траекторию и ограничения групп. MMD сравнивает будущий путь, направление, скорость и положение/скорость стоп с кадрами базы, выбирает подходящий фрагмент, воспроизводит позу и сглаживает переход. Root motion применяется в симуляции; рендер читает готовую позу и деформирует меш на GPU.

Анимационные и render-компоненты находятся на одной основной сущности в одном мире. Unity создаёт технические сущности мешей при baking. Управление, камера, игровые состояния и создание мира принадлежат проекту; GPU-рендер читает результат симуляции.

## Подготовка анимаций

1. Импортируйте клипы и настройте совместимые Rig/Avatar. Зацикливание задаётся через **Loop Time** у анимации.
2. Создайте `MotionBakeConfiguration`: укажите целевой rig, пути корня/стоп, группы и клипы. Для обычного поиска выберите **Motion Matching**, для запуска отдельным запросом — **Action**.
3. Нажмите **Bake Database** в инспекторе. Укажите путь к `.bytes` внутри `Assets`. В проекте разработки можно запекать прямо в Sample: `Assets/Packages/com.collint.mmd/Samples/Databases/Locomotion.bytes`. При установке из Git сохраняйте собственные базы и конфигурации в `Assets`.

Группы задаются именами; числовые маски и Action ID генерируются автоматически. Максимум — 32 группы. База запекается под целевой скелет; персонажи с этим скелетом могут совместно использовать один BlobAsset. Ретаргетинг Humanoid выполняется при запекании. В **Bake Settings → Normalization Groups** можно закрепить группы для расчёта весов поиска; пустой список использует все клипы.

## Подключение к своему миру

Установите системы в существующие группы своего ECS-мира:

```csharp
MmdSystemInstaller.Install(world, fixedTickGroup);
MmdSystemInstaller.InstallInitialization(world, initializationGroup);
MmdPhysicsInstaller.Install(world, fixedTickGroup);
MmdGraphicsInstaller.Install(world, presentationGroup);
```

Installers находятся в `ColInt.MMD.Lifecycle.Helpers`, `.Physics.Helpers` и `.Graphics.Helpers`. Мир должен уже содержать стандартные системы загрузки сцен/префабов, трансформов, физики и Entities Graphics. Проект управляет PlayerLoop, фиксированным шагом и одним singleton `MmdWorldState`, в который передаёт паузу.

Загрузите базу через `MotionDatabaseCodec.Load(textAsset.GetData<byte>())`. На персонаже создайте обязательные данные из таблицы ниже; `MmdPlayback` начните со `Speed = 1f`, `MatchedFrame = -1`. Буфер запроса должен иметь размер `database.Value.FeatureCount`; заполните его до поиска. Скелет и начальную позу задайте по базе. Полный пример инициализации — [MmdSampleCharacterFactory](Samples/Scripts/Character/Helpers/MmdSampleCharacterFactory.cs).

Загруженный runtime BlobAsset принадлежит проекту: разделяйте его между персонажами и освобождайте после завершения jobs и удаления потребителей. Sample освобождает базы после своего мира.

## Основные компоненты

Компоненты расположены в `ColInt.MMD.<Область>.Components`. Данные с пометкой «буфер» добавляются через `AddBuffer<T>`.

| Область / компонент | Назначение и использование |
| --- | --- |
| `Lifecycle / MmdWorldState` | Singleton мира; `IsPaused` останавливает анимационную симуляцию. |
| `Database / MmdDatabase` | Ссылка `Value` на общую базу персонажа. |
| `MotionMatching / MmdSearchSettings` | Частота поиска, маски `RequiredTags` / `ExcludedTags`, штраф переключения и ограничения переходов. `PreferLoop` предпочитает цикл, если в выбранной группе есть подходящий; иначе остаются доступны обычные клипы. `TrajectoryPositionWeight` усиливает приоритет позиций будущего пути (0 и 1 — обычный вес); `TrajectoryDirectionWeight` — его направления. |
| `MotionMatching / MmdSearchQueryElement` (буфер) | Вектор поиска, который игровая система обновляет перед MMD. |
| `Playback / MmdPlayback` | Текущий клип, время, скорость и revision; обычно изменяется системами MMD. |
| `Playback / MmdLocalPose` (буфер) | Выходная локальная поза костей. |
| `RootMotion / MmdRootMotion` | Смещение и вращение корня за такт; потребляется системой движения. |
| `Actions / MmdActionRequest`, `MmdActionState`, `MmdActionWarp` | Запрос действия, результат и необязательная подгонка root motion к целевой точке. Добавьте все три и для обычного playback; `ActionId = 0`, `PreferredClip = -1`, warp выключен. |
| `Lifecycle / MmdFault` | Результат проверки запроса/поиска, в том числе отсутствие кандидатов. |
| `Presentation / MmdSkeletonBone`, `MmdModelMatrix` (буферы) | Иерархия костей и вычисляемые модельные матрицы для рендера. |
| `Playback / MmdInertialization`, `MmdInertializationState`, `MmdInertialPose` (буфер) | Необязательное сглаживание. Добавьте вместе; задайте `PoseDuration`, `StationaryPoseDuration`, `RootDuration` в секундах. `StationaryPoseTags` — маска групп idle для сохранения положения стоп; `0` отключает выравнивание. Не включайте сюда воздушные клипы. |
| `RootMotion / MmdKinematicRoot` | Прямое применение root motion к `LocalTransform`. |
| `Physics / MmdPhysicsRoot` | Применение root motion через `PhysicsVelocity`; нужны компоненты физического тела. Выберите один способ движения. |
| `Physics / MmdGroundMovement`, `MmdTrajectoryEnvironment` | Необязательная обработка поверхности, ступеней и препятствий; требует collider персонажа и мировой физики. |
| `MotionMatching / MmdTrajectoryPoint` (буфер), `MmdSearchQuerySpace` | Три будущие точки в мировых координатах и базис запроса для ограничения пути физикой и отладки. |
| `Graphics / MmdGpuCharacterRender` | Ссылка `Prefab` на GPU-префаб и системное состояние загрузки; добавляется на сущность персонажа. |
| `Graphics / MmdGpuCharacterRenderDisabled` | Добавьте для освобождения визуала; удалите для его повторного создания. |
| `Debugging / MmdTrajectoryDebug` | Добавьте `MmdTrajectoryDebug.Default` для отображения траектории в Editor с включёнными Gizmos. |

## Управление поиском и действиями

`LocomotionSchema` формирует запрос из 27 признаков: путь на 0.2/0.4/0.6 секунды, скорость корня и состояние стоп. Используйте `WriteTrajectory` для простого прогноза и `WritePose` для позы; вариант с `MmdLocalPose` и буфером `MmdPoseFeatureHistory` учитывает отображаемый переход. Свой прогноз движения можно записывать напрямую. Для физического ограничения обновляйте также `MmdTrajectoryPoint` и `MmdSearchQuerySpace` перед `MmdTrajectoryConstraintGroup`.

Разрешайте группы по имени из текущей базы, например:

```csharp
settings.RequiredTags = MotionDatabaseGroups.GetTags(
    ref database.Value, new FixedString64Bytes("Run"));
```

Не сохраняйте сгенерированные ID между запеканиями. Для немедленного поиска после изменения намерения установите `MmdPlayback.SearchCountdown = 0f`; для прерывания удержания перехода — также `TransitionElapsed = settings.MinimumTransitionTime`.

Для отдельного действия заполните `MmdActionRequest.ActionId` через `MotionDatabaseGroups.GetActionId`, задайте `PreferredClip = -1` и увеличьте `Revision`. Читайте `MmdActionState.Completed` / `Rejected`. Возврат к Motion Matching выполняется новым запросом с `ActionId = 0` и новой revision; игровые состояния переключает проект.

GPU-префаб должен иметь `MmdGpuCharacterAuthoring` на корне и `MmdGpuSkinAuthoring` на SkinnedMeshRenderer, тот же скелет, что у базы, и материал `ColInt/MMD/Deformed Lit`. Задайте `MmdGpuCharacterRender.Prefab` до инициализации. Маркер `MmdGpuCharacterRenderInitialized` и ресурсы компонента обслуживаются системами; очистка выполняется при удалении владельца и закрытии мира.

## Примеры и отладка

- [MMDSampleEnvironment](Samples/Scenes/MMDSampleEnvironment.unity) — интеграция с управлением персонажем, локомоцией, приседом, спринтом, слайдом, прыжком, падением, приземлением и Traversal. Эта игровая логика находится в `Samples/Scripts/Character/Movement`.
- [MMDSampleBenchmark](Samples/Scenes/MMDSampleBenchmark.unity) — множество персонажей с одной базой и GPU-префабом. Количество, seed и область движения задаются на **Benchmark Characters**; каждый персонаж получает своё намерение через `MmdSampleCharacterInputOverride`.
- [MmdSampleWorld](Samples/Scripts/Lifecycle/MmdSampleWorld.cs) и [MmdSampleCharacterFactory](Samples/Scripts/Character/Helpers/MmdSampleCharacterFactory.cs) — примеры подключения систем, создания сущностей и освобождения ресурсов.

Управление Sample: **WASD** — движение относительно камеры, **X** — переключение ходьба/бег (по умолчанию бег), **Shift** — спринт и выход из присяда, **Ctrl** — переключение присяда. Нажатие Ctrl во время спринта запускает слайд: на ровной поверхности он тормозит и переходит в присяд, на спуске сохраняет скорость и ускоряется под действием уклона. Скорости и параметры слайда задаются в `MmdSampleCharacterConfiguration`. **Space** — прыжок или Traversal.

В своём проекте используйте собственный мир и игровые системы. Для переноса Sample-геометрии в другой проект выполните **Bake World Physics** на компоненте `MmdSampleWorldPhysicsGeometry`: кеш зависит от версии Unity и физических пакетов.

**Window → Motion Matching for DOTS → Debugger** показывает сущности, текущие клипы, группы и прогресс. В развёрнутой карточке доступны переходы и история последних пяти клипов; **Show in Entities Hierarchy** выбирает сущность для инспекции. Траектория отображается через `MmdTrajectoryDebug`.

Тесты находятся в `Samples/Tests/EditMode` и `Samples/Tests/PlayMode` и запускаются через Unity Test Runner. Производительность измеряйте в Profiler при одинаковых количестве персонажей, seed и частоте симуляции.
