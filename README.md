# Motion Matching for DOTS

MMD (`com.collint.mmd`, namespace `ColInt.MMD`) — пакет скелетной анимации для Unity Entities. Он подбирает анимацию по траектории и позе персонажа, сглаживает переходы, применяет root motion в ECS и поддерживает GPU-деформацию через Entities Graphics.

## Установка

Требуются Unity 6000.3+ и URP. Начиная с версии 0.2.0 ядро и примеры поставляются двумя UPM-пакетами:

| Что установить | Пакет / архив |
| --- | --- |
| Только MMD | `com.collint.mmd` / `com.collint.mmd-0.2.0.tgz` |
| Добавить примеры к установленному MMD | `com.collint.mmd.samples` / `com.collint.mmd.samples-0.2.0.tgz` |
| MMD и примеры одним скачиванием | `mmd-with-samples-0.2.0.zip`, содержащий оба `.tgz` |

Архивы создаются локально командой `python3 Tools/package_release.py` в `artifacts/packages`. Для одного пакета используйте `--package core` или `--package samples`. Папка `artifacts` исключена из Git; скрипт ничего не публикует. Samples требует ядро соответствующей версии и не работает самостоятельно.

В Package Manager выберите **Install package from tarball** и укажите `.tgz`. Для общего комплекта сначала распакуйте ZIP, затем установите архив ядра и архив Samples. Сохраняйте `.tgz` по выбранным путям: проект ссылается на них в `Packages/manifest.json`. Архив ядра не содержит бинарных ресурсов примеров; для установки архивов Git LFS не нужен.

После публикации этих изменений в основную ветку можно также установить пакеты через **Install package from Git URL**. Сначала ядро:

```text
https://github.com/sw1f1s/Motion-Matching-for-DOTS.git?path=Assets/Packages/com.collint.mmd#main
```

Затем, по необходимости, Samples:

```text
https://github.com/sw1f1s/Motion-Matching-for-DOTS.git?path=Assets/Packages/com.collint.mmd.samples#main
```

UPM не поддерживает Git URL в зависимостях `package.json`, поэтому при установке из Git или архивов ядро нужно добавить первым. Оба Git URL используют один репозиторий; Git может скачивать его содержимое целиком. Для скачивания только ядра используйте отдельный `.tgz`. При установке Samples из Git нужен Git LFS. Старый тег `v0.1.0` сохраняет прежний объединённый пакет.

## Состав

- [Ядро](Assets/Packages/com.collint.mmd/README.md): `Runtime` — поиск анимаций, playback, root motion, Physics и Graphics; `Editor` — запекание баз и Debugger.
- [Samples](Assets/Packages/com.collint.mmd.samples/README.md): управление персонажем, камера, игровые состояния, сцены, benchmark, анимации, модели и тесты.

В проекте разработки обе единственные копии находятся в `Assets/Packages`. Unity компилирует их напрямую из Assets; не регистрируйте эти папки одновременно как локальные UPM-зависимости этого же проекта.

## Лицензия

Код — [MIT](LICENSE). Условия сторонних анимаций и моделей указаны в [Third Party Notices пакета Samples](Assets/Packages/com.collint.mmd.samples/Third%20Party%20Notices.md).
