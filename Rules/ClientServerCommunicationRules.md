# MMD Network Integration Rules

Применяется только при явно запрошенной сетевой интеграции. Пакет не требует транспорта или разделения client/server для локальной работы.

- Сетевые интеграции отделять от общих Runtime-областей. Контракты не зависят от конкретного транспорта или проекта-потребителя; серверная симуляция может работать без Graphics.
- Сообщения содержат только данные: не передавать services, World, Unity objects или managed runtime collections. Направление и delivery policy задаются явно.
- Заменяемые input/state updates передавать sequenced, обязательные ordered events — reliable ordered. Идентификаторы схемы сериализации стабильны и уникальны; codec и generated output обновляются через выбранный pipeline.
- Handler проверяет routing и доставляет typed ECS event. Применение состояния, spawn/destroy и batching выполняют системы, без прямой мутации компонентов из transport callbacks.
- Runtime world identity отделять от configuration ID; network entity identity — от prefab/scene identity.
- Авторитетная симуляция проверяет input. Prediction/reconciliation/interpolation используют authoritative tick и ограниченную историю. Tick хранить как ulong, сравнение выполнять единым wrap-safe helper.
- Root motion и перемещение остаются в ECS. Рендер получает согласованный результат и не определяет авторитетное игровое состояние.
- Не создавать boxing/временные коллекции каждый update. Метаданные сериализации кешировать на lifecycle владельца; отменять операции и очищать очереди при его завершении.
- Проверять serialization roundtrip, routing, spawn/update/destroy, порядок загрузки, отмену/reconnect и работу без рендера. Сетевые зависимости объявлять явно при добавлении интеграции.
