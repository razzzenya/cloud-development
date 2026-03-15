# Лабораторная работа №2 - "Балансировка нагрузки"

**Вариант**: №9 - "Кредитная заявка"  
**Алгоритм балансировки**: Weighted Round Robin

**Выполнил**: Куненков Иван, группа 6511

**Предметная область**: Генерация кредитных заявок


## Реализованный функционал

### Основные возможности:
- **API Gateway** на базе Ocelot с алгоритмом балансировки Weighted Round Robin
- **Балансировка с весами 5:3:2** между тремя репликами API
- **3 реплики API сервиса** с оркестрацией через .NET Aspire
- **Генерация кредитных заявок** с реалистичными данными через библиотеку Bogus
- **Трёхуровневое кэширование**: Redis (быстрый доступ) → MinIO (постоянное хранилище) → Генератор
- **Асинхронная обработка** через AWS SNS (LocalStack) с публикацией событий
- **Объектное хранилище MinIO** для персистентного хранения заявок
- **Blazor WebAssembly клиент** для взаимодействия через Gateway
- **Структурное логирование** и телеметрия через OpenTelemetry
- **Мониторинг в реальном времени** через Aspire Dashboard
- **Интеграционные тесты** для проверки всей системы

## 🏗️ Архитектура

```
┌──────────────────────────────────────┐
│  Client.Wasm (Blazor WebAssembly)   │  ← Пользовательский интерфейс
│  - Форма ввода ID                    │
│  - Отображение данных заявки         │
└───────────────┬──────────────────────┘
                │ HTTPS
                ↓
┌──────────────────────────────────────┐
│   CreditApp.ApiGateway (Ocelot)      │  ← API Gateway
│   - Weighted Round Robin (5:3:2)     │
│   - Маршрутизация запросов           │
│   - Структурное логирование          │
└───────────────┬──────────────────────┘
                │
        ┌───────┼───────────┐
        ↓       ↓           ↓
┌─────────┐ ┌─────────┐ ┌─────────┐
│ API-0   │ │ API-1   │ │ API-2   │  ← 3 реплики API
│ (вес 5) │ │ (вес 3) │ │ (вес 2) │
│ :7170   │ │ :7171   │ │ :7172   │
└────┬────┘ └────┬────┘ └────┬────┘
     │           │           │
     └───────────┴───────────┘
                 ↓
        ┌─────────────────┐
        │  Redis Cache    │  ← Общий кэш
        │  TTL: 10 минут  │
        └────────┬────────┘
                 │
        ┌────────┴────────┐
        │   AWS SNS       │  ← Очередь сообщений
        │  (LocalStack)   │
        └────────┬────────┘
                 ↓
        ┌─────────────────┐
        │  FileService    │  ← Сервис файлов
        │  + MinIO        │  ← Постоянное хранилище
        └─────────────────┘
                 ↑
        ┌────────┴────────┐
        │ Aspire AppHost  │  ← Оркестрация
        │ + Dashboard     │
        └─────────────────┘
```

## 📁 Структура проекта

```
cloud-development/
├── CreditApp.AppHost/                    # 🎯 Aspire orchestrator
│   ├── Program.cs                        # Конфигурация: 3 реплики + Gateway + FileService
│   └── localstack-init.sh                # Инициализация SNS топиков
│
├── CreditApp.ApiGateway/                 # 🌐 API Gateway (Лаб. №2)
│   ├── LoadBalancing/
│   │   └── WeightedRoundRobinLoadBalancer.cs  # Алгоритм балансировки
│   ├── Program.cs                        # Ocelot + Service Discovery
│   ├── ocelot.json                       # Конфигурация маршрутов
│   └── appsettings.json                  # Имена сервисов и веса
│
├── CreditApp.Api/                        # 🔧 REST API (3 реплики)
│   ├── Controllers/
│   │   └── CreditController.cs           # GET /api/credit?id={id}
│   ├── Services/
│   │   ├── CreditGeneratorService/
│   │   │   └── CreditApplicationGeneratorService.cs  # Генерация + кэш + MinIO
│   │   └── SnsPublisherService/
│   │       └── SnsPublisherService.cs    # Публикация в SNS
│   └── Program.cs                        # Redis, SNS, CORS, Swagger
│
├── CreditApp.FileService/                # 📁 Сервис файлов
│   ├── Controllers/
│   │   ├── FilesController.cs            # Работа с файлами
│   │   └── NotificationController.cs     # SNS webhook
│   ├── Services/
│   │   └── MinioStorageService.cs        # Работа с MinIO
│   └── Program.cs                        # MinIO клиент
│
├── CreditApp.ServiceDefaults/            # ⚙️ Общие настройки
│   └── Extensions.cs                     # OpenTelemetry, health checks
│
├── CreditApp.Domain/                     # 📦 Модели данных
│   └── Entities/
│       └── CreditApplication.cs          # Модель кредитной заявки
│
├── CreditApp.Test/                       # 🧪 Интеграционные тесты
│   └── IntegrationTest.cs                # End-to-End тесты всей системы
│
├── Client.Wasm/                          # 💻 Blazor WASM клиент
│   ├── Components/
│   │   ├── DataCard.razor                # UI для запроса заявок
│   │   └── StudentCard.razor             # Информация о студенте
│   └── wwwroot/
│       └── appsettings.json              # Адрес Gateway
└── 📄 README.md                          # Этот файл
```


## 📸 Скриншоты!

![aspire](https://github.com/user-attachments/assets/c49d5de0-0afb-4105-b9da-bbc5fe76c9da)
![client](https://github.com/user-attachments/assets/8d4dd124-9589-4562-b421-d0e914a0cc8a)
![logs](https://github.com/user-attachments/assets/120d9b27-d140-429a-b574-31c1c3c0e092)
