# FabulousScheduler — отчёт по багам и неточностям

> Дата: 2026-06-17 · Ветка: `FS-11`
> Источник: статический анализ кода (собрать/прогнать тесты не удалось — в окружении нет .NET SDK).
> Нумерация (B1–B15) совпадает с обсуждением. Идентификаторы/код — на английском.

## Легенда статусов
- 🔴 Критичный · 🟠 Высокий · 🟡 Средний · 🟢 Низкий / latent
- **Status:** `open` — не исправлено · `fixed (docs)` — поправлено в документации в этой сессии

---

## 🔴 Критичные

### B1 — Пример в README регистрирует задачи до `RunScheduler()` (падение)
- **Status:** fixed (docs)
- **Где:** `README.md` (QuickStart); контракт в `src/FabulousScheduler/Recurring/RecurringJobManager.cs:159-162`, `src/FabulousScheduler/Queue/QueueJobManager.cs:133-136`
- **Суть:** README описывал порядок «SetConfig → Register → RunScheduler», но `InternalRegisterJob` бросает `SchedulerNotRunnableException`, пока `_scheduler == null`, а `_scheduler` создаётся только в `RunScheduler()`. Пример из README кидал исключение. Рабочие сэмплы делают `RunScheduler()` до `Register()`.
- **Исправление:** порядок в README приведён к рабочему (`RunScheduler` до `Register`), добавлено предупреждение про оба исключения, `new Config(...)` → `new Configuration(...)`. Код не менялся.

### B2 — Исключение в обработчике `JobResultEvent` навсегда теряет слот семафора
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Abstraction/BaseCronScheduler.cs:151-156`; `src/FabulousScheduler.Queue/Abstraction/BaseQueueScheduler.cs:72-77`
- **Суть:**
  ```csharp
  var res = await @job.ExecuteAsync();
  if (_inProgress.TryRemove(@job.ID, out var tup))
      JobResultEvent?.Invoke(ref tup.Item1, ref res); // если бросит исключение...
  _jobExecutorLimiter.Release(1);                      // ...Release не выполнится
  ```
  `Release` не в `finally`. Если пользовательский обработчик бросит исключение, слот не вернётся (и исключение не наблюдается — fire-and-forget `StartNew`). После `MaxParallelJobExecute` таких случаев планировщик встаёт намертво.
- **Исправление:** обернуть тело задачи в `try/finally` и вызывать `Release(1)` в `finally`.

### B3 — Гонка в `CreateTask`: результат быстрых задач теряется
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Abstraction/BaseCronScheduler.cs:143-159`; `src/FabulousScheduler.Queue/Abstraction/BaseQueueScheduler.cs:64-80`
- **Суть:** задача стартует через `StartNew`, а `_inProgress.TryAdd(...)` выполняется уже **после** старта. Если задача завершится раньше `TryAdd`, внутренний `TryRemove` ничего не найдёт → `JobResultEvent` не вызовется (результат потерян), запись зависнет в `_inProgress`. Дополнительно `StartNew(async …)` возвращает `Task<Task>` — в `_inProgress` кладётся «развёрнутая» обёртка, завершающаяся раньше реальной работы.
- **Исправление:** добавлять в `_inProgress` **до** запуска задачи; использовать `Task.Run`/`.Unwrap()` вместо `StartNew(async …)`.

---

## 🟠 Высокие

### B4 — Очередь не завершается при остановке; `NextAsync()` игнорирует отмену
- **Status:** open
- **Где:** `src/FabulousScheduler.Queue/Abstraction/BaseQueueScheduler.cs:51-58`; `src/FabulousScheduler.Queue/Interfaces/IQueue.cs:17`; `src/FabulousScheduler.Queue/Queues/InMemoryQueue.cs:63-76`
- **Суть:** цикл ждёт `Queue.NextAsync()`, а интерфейс не принимает `CancellationToken`. В `InMemoryQueue` при пустой очереди возвращается `TaskCompletionSource`, завершающийся только при `Enqueue`. На `Dispose()`/`Cancel()` цикл зависает навсегда → утечка потока/задачи.
- **Исправление:** добавить `CancellationToken` в `IQueue.NextAsync`, прокидывать токен планировщика, регистрировать отмену TCS.

### B5 — `Dispose()` рушится на работающей задаче `_mainLoop`
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Abstraction/BaseCronScheduler.cs:170-176`; `src/FabulousScheduler.Queue/Abstraction/BaseQueueScheduler.cs:83-89`
- **Суть:** `_mainLoop?.Dispose()` на незавершённой задаче бросает `InvalidOperationException`. Также `_cancellationTokenSource.Dispose()` вызывается сразу после `Cancel()`, а цикл продолжает обращаться к `.Token` → `ObjectDisposedException`.
- **Исправление:** дождаться завершения цикла после `Cancel()` (например, `await`/`Wait` с таймаутом) и только потом освобождать ресурсы; не диспозить незавершённую задачу.

### B6 — Неверный `[Flags]` на enum'ах состояний/причин
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Enums/JobStateEnum.cs:3`; `src/FabulousScheduler.Queue/Enums/QueueJobStateEnum.cs:3`; `src/FabulousScheduler.Recurring/Enums/JobFailEnum.cs:3`; `src/FabulousScheduler.Queue/Enums/QueueJobFailEnum.cs:6`
- **Суть:** значения последовательные (0,1,2,3,4), не степени двойки. С `[Flags]` `Waiting | Running == Sleeping`. Атрибут семантически неверен; маскируется тем, что сравнения идут через `==`/`is`.
- **Исправление:** убрать атрибут `[Flags]` со всех четырёх enum'ов.

---

## 🟡 Средние

### B7 — `InMemoryQueue` использует `TaskCompletionSource` без `RunContinuationsAsynchronously`
- **Status:** open
- **Где:** `src/FabulousScheduler.Queue/Queues/InMemoryQueue.cs:39,53` (создание TCS — `:69`)
- **Суть:** `SetResult` вызывается под `lock(_lock)`. Продолжения ожидающего `NextAsync` (тело цикла планировщика) выполняются синхронно на потоке, вызвавшем `Enqueue`/`Register`, с повторным входом в `_lock` («continuation hijacking»).
- **Исправление:** создавать TCS с `TaskCreationOptions.RunContinuationsAsynchronously`.

### B8 — `InMemoryQueue.Enqueue(IEnumerable)` дважды перечисляет последовательность
- **Status:** open
- **Где:** `src/FabulousScheduler.Queue/Queues/InMemoryQueue.cs:44-61`
- **Суть:** `jobs.Take(take)` и `jobs.Skip(take)` перечисляют `jobs` дважды — для ленивых/одноразовых `IEnumerable` задачи могут создаться дважды или потеряться.
- **Исправление:** материализовать вход один раз (`var arr = jobs as IReadOnlyList<...> ?? jobs.ToArray();`).

### B9 — Вечно падающая recurring-задача крутится без паузы
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Abstraction/BaseRecurringJob.cs:129-133` (finally) + `UpdateState` `:165-182`
- **Суть:** `SleepDuration` отсчитывается только от **успешного** запуска. После фейла `UpdateState` сразу возвращает `Ready`, поэтому постоянно падающая задача перезапускается на максимальной скорости (CPU-spin + флуд `JobResultEvent`). Поведение задокументировано как «by design», но это footgun.
- **Исправление:** опционально стартовать sleep и после неуспеха (backoff), либо явно описать в доках (описано в `docs/Recurring.md`).

### B10 — Нет Unregister; `_registeredJob` растёт бесконечно
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Abstraction/BaseCronScheduler.cs:20,98-103,163-166`
- **Суть:** задачи никогда не удаляются из `_registeredJob`; `Dispose()` у самой задачи не убирает её из словаря — утечка памяти у долгоживущих планировщиков, плюс `O(n)` скан в `TryScheduleJobs`.
- **Исправление:** добавить `Unregister(Guid)`/удаление `Disposed`-задач из реестра.

---

## 🟢 Низкие / latent

### B11 — `State`-getter с побочным эффектом + чтение `_state` вне лока
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Abstraction/BaseRecurringJob.cs:34-41, 165-181`
- **Суть:** getter `State` вызывает `UpdateState()`, который **мутирует** состояние; строки `167-168` читают `_state` вне `_lock` — гонка. Также в `UpdateState` оба `if` (`:172` и `:177`) могут отработать подряд.
- **Исправление:** вынести продвижение состояния из getter'а; читать `_state` под локом.

### B12 — Возможная утечка слота семафора при неудачном `TryDequeue`
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Abstraction/BaseCronScheduler.cs:130-135`
- **Суть:** слот берётся `WaitAsync`, затем `if (_queue.TryDequeue(...))`; при неудаче `Dequeue` слот не освобождается. Для одиночного консьюмера сейчас маловероятно, но не защищено.
- **Исправление:** в `else`-ветке делать `Release(1)`.

### B13 — `InMemoryQueue.Count` читается без лока и не учитывает «в полёте»
- **Status:** open
- **Где:** `src/FabulousScheduler.Queue/Queues/InMemoryQueue.cs:16`
- **Суть:** `_queue.Count` без `lock` (warning явно подавлен); отражает только бэклог, не задачи в работе.
- **Исправление:** читать под локом; задокументировать семантику.

### B14 — Синхронные задачи исполняются через блокирующий `.Result`
- **Status:** open
- **Где:** `src/FabulousScheduler.Recurring/Abstraction/BaseRecurringJob.cs:108`; `src/FabulousScheduler.Queue/Abstraction/BaseQueueJob.cs:82`
- **Суть:** `ActionJob().Result` — анти-паттерн. Здесь почти безвреден (Task уже завершён к моменту вызова), но хрупко.
- **Исправление:** для sync-ветки не оборачивать в Task/не звать `.Result`.

### B15 — Незакрытые TODO и несогласованности
- **Status:** open
- **Где / суть:**
  - `src/FabulousScheduler.Recurring/Interfaces/IRecurringJob.cs:31-40` — `ExecuteAsync` без описания, битый `<see cref="JobStateEnumJobStateEnuming"/>`.
  - `src/FabulousScheduler.Queue/Result/JobFail.cs:11` (`: Exception`) против `src/FabulousScheduler.Recurring/Result/JobFail.cs:9` (обычный класс) — несогласованность дизайна между подсистемами.
  - Опечатка дефолтного имени `"anonimouse"`: `src/FabulousScheduler/Recurring/Internal/RecurringJob.cs:10`, `src/FabulousScheduler/Queue/Internal/QueueJob.cs:10`.
  - TODO-комментарии: `BaseRecurringJob.cs:155` (`DisposeAsync`), `BaseQueueJob.cs:118` (`Dispose`).

---

## Неточности в документации (на момент сессии)

| # | Где | Проблема | Статус |
|---|-----|----------|--------|
| D1 | `README.md` QuickStart | Неверный порядок `Register`/`RunScheduler` (см. B1) | fixed (docs) |
| D2 | `README.md` QuickStart | `new Config(...)` — нет такого типа, корректно `Configuration` | fixed (docs) |
| D3 | `README.md` Contents | Ссылки на несуществующие `docs/General.md`, `docs/RecurringScheduler.md`, `docs/QueueScheduler.md` | fixed (docs) → `Core.md` / `Recurring.md` / `QueueBased.md` |
| D4 | `docs/Core.md`, `docs/Recurring.md`, `docs/QueueBased.md` | Были пустыми оглавлениями без содержимого | fixed (docs) — заполнены |
| D5 | `project_map.md` | `IQueue` указан с `Enqueue(jobs)` и описан как часть интерфейса — фактически `Enqueue(IEnumerable)` есть только в `InMemoryQueue`, не в `IQueue` | open |
| D6 | `project_map.md` | Файл назван `BaseRecurringScheduler.cs`, фактически на диске — `BaseCronScheduler.cs` | open |
| D7 | Дефолтный менеджер `QueueJobManager` | `Attempts` не пробрасывается через `Register` (всегда `null`); ретрай доступен только при ручной сборке задач | задокументировано в `docs/QueueBased.md` |
