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

Sample входит в пакет. При установке через UPM он доступен в `Packages/com.collint.mmd/Samples`; в проекте разработки — в `Assets/Packages/com.collint.mmd/Samples`:
сцена `Scenes/MMDSampleEnvironment.unity`, персонаж, анимации, конфигурации,
Editor-инструменты и тесты. Input System 1.19.0 устанавливается как зависимость.
Для запуска тестов нужен Unity Test Framework.
В другом проекте выберите объект `Enviroment` в Sample-сцене и нажмите **Bake World Physics**: кеш физики зависит от версии Unity и установленных пакетов.

Свет и постобработка Sample находятся в `Samples/Configurations/Environment`, URP pipeline и renderer — в `Samples/Configurations/Rendering`. В другом URP-проекте назначьте `MmdSampleRenderPipeline` в Graphics и активном уровне Quality, чтобы получить тот же вид сцены. Это настройка проекта; пакет не меняет её автоматически. В сцене **Atmosphere** управляет небом, облаками, туманом и солнечными лучами, **Global Volume** — цветокоррекцией, ACES, bloom и остальными эффектами. Направление солнца задаёт **Directional Light**.

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

Группы задаются именами; числовые маски и Action ID генерируются автоматически. Максимум — 32 группы. База запекается под целевой скелет; персонажи с этим скелетом могут совместно использовать один BlobAsset. Ретаргетинг Humanoid выполняется при запекании. В **Bake Settings → Normalization Groups** можно закрепить группы для расчёта весов поиска; пустой список использует все клипы. В Sample сохранён исходный набор групп, чтобы добавление приседа не меняло подбор анимаций стоя.

## Подключение к своему миру

Нужны Unity Entities, Entities Graphics, Unity Physics и URP; запекание в Editor использует стандартный `Task`, без сторонних async-пакетов. Установите системы в существующие группы своего мира:

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
| `Playback / MmdInertialization`, `MmdInertializationState`, `MmdInertialPose` (буфер) | Необязательное сглаживание. Добавьте вместе; задайте `PoseDuration`, `StationaryPoseDuration`, `RootDuration` в секундах. |
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

## Sample и отладка

Откройте [MMDSampleEnvironment](Samples/Scenes/MMDSampleEnvironment.unity). Sample содержит манекен, базу локомоции и физическое окружение. WASD — движение относительно камеры, Shift — бег, Left Ctrl — присед (удержание), Space — прыжок или Traversal к препятствию, мышь — вращение камеры, P — пауза. В idle персонаж сохраняет направление при вращении камеры.

`MmdSampleBootstrap` создаёт пример мира и запускает окружение, спавнеры, камеру и input. `MmdSampleCharacterConfiguration` разделён на раскрывающиеся разделы `Assets`, `Locomotion`, `Transitions`, `Jump`, `Fall`, `Landing`, `Crouch` и `Traversal`. Группы прыжка и traversal находятся в `Groups`, группы приземления — в `LightGroups` и `HeavyGroups`; положение спавна берётся из Transform спавнера. Это пример интеграции для замены своими системами проекта.

В Sample **Space** запускает прыжок; сход с края включает падение, контакт с землёй — лёгкое или тяжёлое приземление. Высота прыжка, управление в воздухе, порог сильного удара и время восстановления задаются в `MmdSampleCharacterConfiguration`. Отталкивание выполняется в тот же такт, что и Space; клип начинается с авторского момента отрыва стоп (`Jump.TakeoffTime` — запасная точка входа). Нажатие перед контактом с землёй буферизуется на 0,16 с; после схода с края остаётся 0,1 с для прыжка. Новый прыжок прерывает Landing, лёгкое восстановление по умолчанию занимает 0,08 с.

Присед реализован в `Movement/CrouchState`: вход → движение/idle в приседе → выход. В конфигурации задаются скорость, высота капсулы и длительность сглаживания; выйти из приседа можно только при свободном месте над головой. `Crouch Idle/Move/Turn/Start` используют Motion Matching, `Crouch Enter/Exit` — Action. На месте проигрываются авторские переходы стоя ↔ присед; на ходу переход сглаживается в старты приседа и обратно в обычную локомоцию, сохраняя движение. Отдельных клипов перехода из шага/бега в исходном Neutral-наборе нет.

В `Character/Input` находятся команды игрока и чтение клавиатуры; `Movement/LocomotionState` выбирает локомоцию, `Movement/Trajectory` прогнозирует путь и получает скорость из физики, `Movement/Facing` управляет поворотом. Подготовка поискового запроса и установка систем находятся в `Movement/Shared`.

Вся логика состояний и перемещения находится в `Samples/Scripts/Character/Movement` и не зависит от `PedFeature` проекта. `MmdSampleJump`, `MmdSampleFall`, `MmdSampleLanding` задают доступные действия; `MmdSampleState<T>` обозначает активное состояние. Переход запрашивается через `MmdSampleStateTransition.Request<T>(priority)` и проходит отдельные start/update/finish-фазы. Сущность и её физика остаются владельцами положения, а GPU-рендер использует готовую позу.

Группы `Jump Stand/Walk/Run` и `Land Stand/Walk/Run Light/Heavy` используют **Action**, `Fall` — **Motion Matching** с зацикленным клипом. Клипы приземления в Sample обрезаны до контакта с землёй в FBX Import Settings, чтобы не проигрывать начальное падение повторно. Пустое имя `Fall.Group` отключает Sample-состояния для собственного варианта управления.

**Window → Motion Matching for DOTS → Debugger** показывает сущности, текущие клипы/группы и прогресс. В деталях доступны переходы и история последних пяти клипов; **Show in Entities Hierarchy** выбирает сущность для инспекции.

Traversal полностью находится в `Samples/Scripts/Character/Movement/TraversalState`: отдельные состояния Mantle, Climb, Vault, Hurdle и Catch-варианты. Space проверяет высоту, ширину, толщину и свободное место над препятствием; без подходящей цели выполняется обычный прыжок. Во время падения Space запускает доступный зацеп. На время Traversal ECS подгоняет авторскую траекторию root motion к высоте, ближнему краю и выходу с препятствия. Vault/Hurdle используют капсулу корпуса с поднятой нижней частью на время действия; затем возвращаются обычная капсула и физика. Подготовительный разбег клипа сокращается по расстоянию до стены, бег получает дополнительную дистанцию проверки, а нажатие сохраняется на 0,35 с для зацепа из прыжка. Потеря препятствия или новая помеха прерывает действие в Fall. Пределы высот, дистанция и группы настраиваются в SampleCharacterConfiguration; пустая `Traversal.Groups.Mantle` отключает эти состояния.
