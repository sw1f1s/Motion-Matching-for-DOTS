# Motion Matching for DOTS

Проект разработки Unity 6000.3.18f1 и пакет `com.collint.mmd`.

## Установка через Package Manager

В Unity откройте **Window → Package Management → Package Manager → + → Install package from git URL** и вставьте:

```text
https://github.com/sw1f1s/Motion-Matching-for-DOTS.git?path=Assets/Packages/com.collint.mmd#v0.1.0
```

Нужны Git и Git LFS. Вместе с пакетом устанавливается `Samples` со сценой, анимациями и настройками. Для получения текущей версии разработки используйте `#main`.

## Проект разработки

- `Assets/Packages/com.collint.mmd/Runtime` — Motion Matching, анимация, физика и GPU-рендер.
- `Assets/Packages/com.collint.mmd/Editor` — запекание баз и Debugger.
- `Assets/Packages/com.collint.mmd/Samples` — пример со сценой, персонажем, анимациями, настройками и тестами. Входит в пакет и доступен сразу после установки.
- `Rules`, `AGENTS.md`, `.editorconfig` — правила работы с кодом и агентами.

Исходники пакета редактируются напрямую в `Assets/Packages/com.collint.mmd`. `package.json` используется при установке из Git; зависимости проекта разработки указаны в его Package Manager.

Откройте проект в Unity. `MmdProjectSetup` проверит зависимости и настроит Sample-сцену для запуска. Все изменения примера делайте прямо в `Assets/Packages/com.collint.mmd/Samples`.

Запуск: `Samples/Scenes/MMDSampleEnvironment.unity` → Play. WASD — движение, Shift — бег,
Left Ctrl — присед, Space — прыжок/Traversal, мышь — камера, P — пауза.
Дебаг: Window → Motion Matching for DOTS → Debugger.

[Установка и API плагина](Assets/Packages/com.collint.mmd/README.md).
[Лицензия кода MIT](LICENSE).
[Условия сторонних ресурсов](Assets/Packages/com.collint.mmd/Third%20Party%20Notices.md).

Git LFS используется для FBX, текстур и запечённых баз. Установите Git LFS перед
клонированием/установкой из Git URL. Unity-кеши игнорируются.

Для агентов доступны MCP for Unity и Unity Pipeline. Проверяйте имя проекта перед
каждой серией команд, чтобы не направить изменения в другую открытую Unity-сессию.
Запустите EditMode и PlayMode тесты через Test Runner или MCP.
Проверяйте чистую установку пакета вместе с Sample после изменения зависимостей.
