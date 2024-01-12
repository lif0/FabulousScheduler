<div align="center">
<img src="./assets/logo.png" width="50%" height="auto" > 
<h2 align="center">✨High performance cron & queue scheduler✨</h2>
<h3 align="center">🌪️Currently a WIP and in Active development.</h3>



<!--![build](https://github.com/lif0/FabulousScheduler/actions/workflows/github-actions-build.yml/badge.svg) ![tests](https://github.com/lif0/FabulousScheduler/actions/workflows/github-actions-build.yml/badge.svg) -->
![build](https://github.com/lif0/FabulousScheduler/actions/workflows/github-actions-build.yml/badge.svg) ![tests](https://github.com/lif0/FabulousScheduler/actions/workflows/github-actions-tests.yml/badge.svg) ![publish](https://github.com/lif0/FabulousScheduler/actions/workflows/github-actions-push-nuget.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/FabulousScheduler.svg)](https://www.nuget.org/packages/FabulousScheduler) [![Downloads](https://img.shields.io/nuget/dt/FabulousScheduler.svg)](https://www.nuget.org/packages/FabulousScheduler) [![license](https://img.shields.io/github/license/lif0/FabulousScheduler.svg)](https://github.com/lif0/FabulousScheduler/blob/main/LICENSE)
<!--[![build](https://github.com/lif0/FabulousScheduler/workflows/build/badge.svg?branch=main)](https://github.com/lif0/FabulousScheduler/actions?query=branch%3Amain) -->

<h>If you have feature request feel free to open an [Issue](https://github.com/lif0/FabulousScheduler/issues/new/choose)</h4>
</div>

<br />

## 📖 Contents

- [Motivation](#motivation)
- [Related works](#related-works) (future)
- [Features](#features)
- [Usage](#usage)
    - [Requirements](#requirements)
    - [Installation](#installation)
    - [Examples](#examples)
- [Benchmarks](#benchmarks) (future)
    - [Performance](#performance) (future)
    - [Hit ratio](#hit-ratio) (future)
- [Contribute](#contribute) (future)
- [License](#LICENSE)

## 🫵 Who is this library for
I have developed this library for cases where you need to launch a large number of tasks without stopping. When you need to do a lot of action in parallel and on a competitive basis in a timely manner. I used this library where I had to grab the site pages at the same time once a minute and it proved to be stable.
<br> In which projects it will be perform best❓<br>
- If you need to grab a site pages without stopping <br>
- If you need to get price quotes from exchanges for a large count of shares by API
- And in many other projects where you have to do something on time, in large quantities and with a certain frequency

## 🚀 Features <a id="features" />
- Default queue scheduler
- Расписать ридми с объяснениями что за такой JobResult
- Добавить возвращение Джобы
- Добавить перегрузку с Именем, Категорией
- Possibility set a custom thread pool to prioritize a jobs
- Cover all project with unit tests

## 📚 Usage <a id="usage" />
### 📋 Requirements <a id="requirements" />

- dotnet 6.0+

### 🛠️ Installation <a id="installation" />

```shell
dotnet add package FabulousScheduler
```

### ✏️ Examples <a id="examples" />

**Use flow**

FabulousScheduler uses a builder pattern that allows you to conveniently create a cron or queue jobs for executing

```csharp
using FabulousScheduler.Cron;

// Init CronJobManager with default config
CronJobManager.Init();

// Register callback for job's result
CronJobManager.CallbackEvent += result =>
{
    Console.WriteLine("[{0:hh:mm:ss}] jobID: {1} IsSuccess: {2}", DateTime.Now, result.Id, result.IsSuccess, result.IsFail);
};

// Registe the job
CronJobManager.Register(
    action: () =>
    {
        Console.WriteLine();
        throw new Exception("some err");
    },
    sleepDuration: TimeSpan.FromSeconds(1) 
);
// The job will fall asleep for 1 second after  success completion, then it will wake up and will be push the job pool

Thread.Sleep(-1);
```

**Cache**
```csharp

```

## 📄 LICENSE <a id="LICENSE" />
### GPL3 LICENSE SYNOPSIS

**_TL;DR_*** Here's what the GPL3 license entails:

```markdown
1. Anyone can copy, modify and distribute this software.
2. You have to include the license and copyright notice with each and every distribution.
3. You can use this software privately.
4. You can use this software for commercial purposes.
5. Source code MUST be made available when the software is distributed.
6. Any modifications of this code base MUST be distributed with the same license, GPLv3.
7. This software is provided without warranty.
8. The software author or license can not be held liable for any damages inflicted by the software.
```

More information on about the [LICENSE can be found here](http://choosealicense.com/licenses/gpl-3.0/)