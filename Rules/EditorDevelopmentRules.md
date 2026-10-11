# MMD Editor Development Rules

Применяется к `Assets/Packages/com.collint.mmd/Editor`, `Samples/Editor` и настройке проекта в `Assets/Editor`.

- Editor tooling находится в Editor assembly; Runtime не зависит от UnityEditor, Editor или Sample Editor.
- Основной Editor содержит baking и Debugger. Инструменты геометрии и игровой сцены находятся в Sample Editor. Не добавлять зависимости от конкретного проекта-потребителя.
- Новые окна и инспекторы оформлять через UI Toolkit. UI отображает данные и запускает команды; обработка данных находится в helpers/controllers. UI проверять вручную, non-UI contracts/lifecycle — тестами.
- Изменения сцены поддерживают Undo и не выполняются в Play Mode. Каждая группа изменений завершает собственную Undo group до await.
- Для больших сериализованных кешей использовать `Undo.RegisterCompleteObjectUndo`: diff byte-массивов через `Undo.RecordObject` расходует чрезмерную память.
- Длительные операции используют Task, показывают прогресс, возвращают управление main thread и поддерживают отмену при смене/закрытии сцены, входе в Play Mode, reload и уничтожении владельца.
- Отмена или ошибка сохраняет предыдущую рабочую базу/кеш. Заменять результат после успешной сборки, валидации и проверки неизменности источника.
- Пользовательские результаты сохранять в Assets; в проекте разработки можно писать прямо в `Assets/Packages/com.collint.mmd/Samples`. Установленный Git/registry-пакет не изменять. Временные bake-ассеты имеют отдельную папку и явный cleanup.
- Сохранять GUID, обновлять asset paths после перемещений и проверять сцены, префабы, конфигурации, метаданные Debugger и тесты.
- Отписываться от EditorApplication, UI callbacks и runtime events. Историю телеметрии ограничивать; не перестраивать большие списки каждый editor frame.
- Debugger открывается через Window → Motion Matching for DOTS → Debugger. Список карточек сущностей показывает клип, группу, прогресс; детали раскрываются внутри карточки, история ограничена пятью клипами. Сохранять переход к сущности в Entities Hierarchy.
- ECS diagnostics по умолчанию read-only. Читать по запросу с ограниченной частотой; измерять реальные callbacks через ProfilerMarker, не обходить весь мир каждый editor frame.
- Validation возвращает явный результат; исключение не считается успехом. Generated output вручную не редактировать.
- После изменений проверять компиляцию и Console, affected tests, lifecycle Play Mode и работу изменённого окна/инспектора вручную.
