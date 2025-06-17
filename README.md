# Formation Microservices: Atelier avec .Net 8

Ce projet sert d'atelier à la formation Microservices avec DotNet 8 dispensée par le cabinet Demkada Academy.

## Architecture du Projet

Le projet est composé de 4 composants principaux :

1. **ProductService** - Service permettant de créer et gérer des produits
2. **OrderService** - Service permettant de créer des commandes et publier des événements
3. **NotificationService** - Service consommant les événements pour envoyer des notifications
4. **API Gateway (Ocelot)** - Passerelle API centralisant les requêtes

### Diagramme de Flux de Données

```
┌─────────────┐     ┌─────────────────────┐     
│             │     │                     │     
│   Client    │────▶│    API Gateway      │     
│             │     │     (Ocelot)        │     
└─────────────┘     └─────────┬───────────┘     
      HTTP/REST               │                  
                              │                  
                              │ HTTP/REST        
                              │                  
                 ┌────────────│───────────────────────────────────────────────┐
                 │            │                                               │             
                 ▼            ▼                                               │
┌─────────────────┐    ┌─────────────────────────┐    ┌─────────────────┐     │
│                 │    │      OrderService       │    │                 │     │
│ ProductService  │◀───┤                         │───▶│    RabbitMQ     │     │
│                 │    │  ┌─────────┐ ┌────────┐ │    │  Message Broker │     │
└─────────────────┘    │  │ Commands│ │Queries │ │    └────────┬────────┘     │
      ▲                │  └────┬────┘ └───┬────┘ │            │               │
      │                │       │          │      │            │               │
      │                │       ▼          ▼      │            │ AMQP          │
      │                │  ┌─────────────────┐    │            │ (MassTransit) │ 
      │                │  │     MediatR     │    │            │               │
      │                │  └────────┬────────┘    │            ▼               │
      │                │           │             │    ┌─────────────────┐     │
      │                │      ┌────┴─────┐       │    │                 │     │
      │                │      │  Sagas   │       │    │NotificationSvc  │ ◀───┘
      │                │      └──────────┘       │    │                 │
      │                └─────────────────────────┘    └─────────────────┘
      │                       │
      └───────────────────────┘
           HTTP/REST avec
         Polly (Resilience)
```

#### Explication du Flux de Données

1. **Client → API Gateway** : Les clients communiquent avec le système via l'API Gateway en utilisant le protocole HTTP/REST. L'API Gateway sert de point d'entrée unique pour toutes les requêtes.

2. **API Gateway → Services** : L'API Gateway (Ocelot) route les requêtes vers les microservices appropriés (ProductService ou OrderService) en utilisant HTTP/REST.

3. **OrderService (CQRS)** : À l'intérieur de l'OrderService, le pattern CQRS est implémenté :
   - Les requêtes HTTP entrantes sont transformées en Commands (pour les opérations d'écriture) ou Queries (pour les opérations de lecture)
   - MediatR dispatche ces Commands/Queries vers les Handlers appropriés
   - Les Handlers exécutent la logique métier et interagissent avec le Repository

4. **OrderService (SAGA)** : Pour les opérations qui nécessitent une coordination entre services :
   - Après la création d'une commande, un OrderCreatedEvent est publié
   - La Saga orchestre le processus en suivant les étapes et en maintenant l'état dans OrderSagaState
   - En cas d'échec, des actions compensatoires sont exécutées pour maintenir la cohérence

5. **OrderService → ProductService** : Lors de la création d'une commande, l'OrderService peut avoir besoin de vérifier les informations des produits auprès du ProductService. Cette communication utilise HTTP/REST avec Polly pour la résilience (retry, circuit breaker).

6. **OrderService → RabbitMQ** : Lorsqu'une commande est créée, l'OrderService publie des événements sur RabbitMQ en utilisant le protocole AMQP via MassTransit :
   - `OrderCreatedEvent` pour la Saga interne
   - `OrderCreated` pour la notification externe

7. **RabbitMQ → NotificationService** : Le NotificationService s'abonne aux événements `OrderCreated` sur RabbitMQ et les consomme via AMQP (MassTransit) pour envoyer des notifications par email.

Cette architecture découplée permet une grande flexibilité et résilience. Si un service est temporairement indisponible, les messages restent dans la file d'attente RabbitMQ et seront traités une fois le service rétabli. L'utilisation de CQRS et SAGA renforce cette résilience en permettant une meilleure séparation des préoccupations et une gestion robuste des transactions distribuées.

## Technologies Utilisées

- **.NET 8** avec Minimal API
- **Docker** et **Docker Compose** pour la conteneurisation
- **Ocelot** comme API Gateway
- **Polly** pour la résilience
- **MediatR** pour l'implémentation du pattern CQRS
- **MassTransit** avec **RabbitMQ** pour la messagerie et l'orchestration des Sagas
- **Swagger** pour la documentation des API
- **Serilog** pour le logging structuré et centralisé

## Patterns de Conception Utilisés

### CQRS (Command Query Responsibility Segregation)

Le pattern CQRS sépare les opérations de lecture (Queries) des opérations d'écriture (Commands) pour optimiser les performances et la scalabilité. Dans notre projet :

- **Séparation des responsabilités** : Les commandes (modifications d'état) sont séparées des requêtes (lectures d'état)
- **Modèles distincts** : Utilisation de DTOs spécifiques pour les commandes et les requêtes
- **Traitement spécialisé** : Handlers dédiés pour chaque commande et requête

Ce pattern est implémenté dans l'OrderService avec :
- **MediatR** comme médiateur pour dispatcher les commandes et requêtes
- Structure de dossiers `/CQRS` avec sous-dossiers `/Commands`, `/Queries`, `/Handlers` et `/DTOs`
- Commandes comme `CreateOrderCommand` et `UpdateOrderStatusCommand`
- Requêtes comme `GetAllOrdersQuery` et `GetOrderByIdQuery`
- Handlers correspondants qui implémentent `IRequestHandler<TRequest, TResponse>` de MediatR

Avantages de cette implémentation :
- **Découplage** : Les composants d'écriture et de lecture évoluent indépendamment
- **Optimisation** : Possibilité d'optimiser séparément les modèles de lecture et d'écriture
- **Testabilité** : Facilite les tests unitaires des commandes et requêtes
- **Extensibilité** : Ajout facile de nouvelles commandes et requêtes sans modifier le code existant

### SAGA Pattern

Le pattern SAGA gère les transactions distribuées à travers plusieurs microservices, en utilisant une séquence d'étapes compensatoires en cas d'échec. Dans notre projet :

- **Coordination des transactions** : Gestion cohérente des opérations qui impliquent plusieurs services
- **Compensation** : Mécanisme de rollback en cas d'échec d'une étape
- **État persistant** : Suivi de l'état de la saga pour permettre la reprise après un crash

Ce pattern est implémenté dans l'OrderService avec :
- **MassTransit** comme framework pour orchestrer les sagas
- Structure de dossiers `/Sagas` avec `/Events` et `OrderSagaState.cs`
- Événements comme `OrderCreatedEvent` et commandes comme `ReserveProductsCommand`
- État de la saga dans `OrderSagaState` qui suit la progression de la transaction

Le flux typique d'une saga dans notre système :
1. Création d'une commande (OrderService)
2. Publication de l'événement `OrderCreatedEvent`
3. Réservation des produits (ProductService) via `ReserveProductsCommand`
4. Notification au client (NotificationService) via `OrderCreated`
5. En cas d'échec à n'importe quelle étape, exécution d'actions compensatoires

Avantages de cette implémentation :
- **Cohérence éventuelle** : Garantit la cohérence des données à terme, même en cas de défaillance
- **Résilience** : Le système peut récupérer après des pannes
- **Traçabilité** : L'état de la saga permet de suivre la progression des transactions
- **Découplage** : Les services communiquent via des événements sans couplage direct

### API Gateway (Ocelot)

Le pattern API Gateway fournit un point d'entrée unique pour tous les clients. Dans ce projet, nous utilisons **Ocelot** comme implémentation de ce pattern, qui offre :

- **Routage** : Redirection des requêtes vers les microservices appropriés
- **Agrégation** : Possibilité de combiner plusieurs appels de services en une seule réponse
- **Transformation des requêtes/réponses** : Modification des données avant/après l'appel aux services
- **Load Balancing** : Distribution des requêtes entre plusieurs instances d'un même service
- **Sécurité centralisée** : Authentification et autorisation au niveau de la passerelle

La configuration du routage est définie dans le fichier `ocelot.json`, qui spécifie comment les requêtes entrantes sont mappées aux services internes.

### Circuit Breaker (Polly)

Le pattern Circuit Breaker empêche une application d'effectuer des opérations susceptibles d'échouer. Dans notre implémentation avec **Polly** :

- **Détection des défaillances** : Surveillance des échecs dans les appels HTTP
- **Prévention des défaillances en cascade** : Arrêt temporaire des appels après un certain nombre d'échecs
- **Récupération automatique** : Tentatives de reconnexion après une période de "refroidissement"
- **Fallback** : Possibilité de définir un comportement alternatif en cas d'échec

Ce pattern est implémenté dans l'OrderService pour les appels au ProductService, avec une politique de retry configurée.

### Message Broker (RabbitMQ avec MassTransit)

Le pattern Message Broker permet une communication asynchrone entre services via un intermédiaire. Notre implémentation utilise :

- **RabbitMQ** comme broker de messages
- **MassTransit** comme abstraction pour simplifier l'utilisation de RabbitMQ
- **Publication/Souscription** : L'OrderService publie des événements, le NotificationService y souscrit
- **Découplage** : Les services n'ont pas besoin de connaître les détails d'implémentation des autres
- **Résilience** : Les messages peuvent être mis en file d'attente si un service est indisponible

Ce pattern est essentiel pour maintenir la cohérence des données entre services sans couplage fort.

### Repository Pattern

Le pattern Repository fournit une abstraction de la couche de données. Dans notre projet :

- **Séparation des préoccupations** : Isolation de la logique d'accès aux données
- **Testabilité** : Facilite les tests unitaires grâce aux interfaces
- **Flexibilité** : Permet de changer l'implémentation sans modifier le code client

Chaque service (Product, Order) implémente ce pattern avec des interfaces (IProductRepository, IOrderRepository) et des implémentations concrètes.

### Dependency Injection

Le pattern Dependency Injection est utilisé dans tous les services pour :

- **Inversion de contrôle** : Les dépendances sont fournies aux classes plutôt que créées par elles
- **Couplage faible** : Les classes dépendent d'abstractions plutôt que d'implémentations concrètes
- **Testabilité** : Facilite le mocking des dépendances pour les tests unitaires

.NET 8 fournit un conteneur DI intégré que nous utilisons pour enregistrer et résoudre les dépendances dans chaque service.

### Minimal API

Le pattern Minimal API de .NET 8 est utilisé pour créer des API REST avec un minimum de code :

- **Simplicité** : Réduction du boilerplate code par rapport aux contrôleurs traditionnels
- **Performance** : Optimisé pour les performances avec moins d'overhead
- **Lisibilité** : Structure claire et concise pour définir les endpoints

Ce pattern est particulièrement adapté aux microservices où la simplicité et la performance sont essentielles.

### Containerization

Le pattern Containerization avec Docker permet :

- **Isolation** : Chaque service s'exécute dans son propre environnement isolé
- **Portabilité** : Les conteneurs fonctionnent de manière identique dans tous les environnements
- **Scalabilité** : Facilite le déploiement de plusieurs instances d'un service
- **Orchestration** : Permet l'utilisation d'outils comme Kubernetes pour la gestion des conteneurs

Chaque service possède son propre Dockerfile, et Docker Compose est utilisé pour orchestrer l'ensemble du système.

## Stratégie de Logging et Suivi du Flux de Données

Le projet implémente une stratégie de logging complète pour faciliter le suivi du flux de données à travers les différents microservices. Cette approche permet de tracer le parcours complet d'une requête, depuis son entrée dans le système jusqu'à son traitement final.

### Implémentation du Logging

- **Serilog** est utilisé dans tous les services pour fournir un logging structuré et cohérent
- Les logs sont écrits à la fois dans la console et dans des fichiers texte dans le répertoire `/logs`
- Chaque service a son propre fichier de log avec rotation quotidienne (par exemple, `product-service-20250617.txt`)
- Le format des logs inclut l'horodatage, le niveau de log, et le contexte de l'opération

### Suivi du Flux de Données

Le logging permet de suivre le flux de données complet à travers le système :

1. **API Gateway** : Logs des requêtes entrantes et de leur routage vers les services appropriés
2. **ProductService** : Logs des opérations CRUD sur les produits
3. **OrderService** :
   - Logs des opérations CRUD sur les commandes
   - Logs détaillés de la publication des événements vers RabbitMQ
   - Logs des appels HTTP vers le ProductService avec la politique de résilience Polly
4. **NotificationService** :
   - Logs de la consommation des événements depuis RabbitMQ
   - Logs détaillés du traitement des messages et de l'envoi d'emails

Cette approche permet de :
- **Déboguer** efficacement les problèmes en suivant le parcours complet d'une requête
- **Surveiller** le comportement du système en production
- **Analyser** les performances et identifier les goulots d'étranglement
- **Auditer** les opérations effectuées sur le système

### Accès aux Logs

Les logs sont accessibles de plusieurs façons :
- En temps réel via la sortie console des conteneurs Docker
- Dans les fichiers de log montés dans le volume `/logs` du système hôte
- Via la commande `docker logs <container-name>` pour voir les logs d'un conteneur spécifique

Pour suivre les logs en temps réel :
```bash
# Suivre les logs d'un service spécifique
docker logs -f product-service

# Suivre les logs de tous les services
docker-compose logs -f
```

## Fonctionnalités

1. Création de produits via le ProductService
2. Création de commandes via l'OrderService qui publie un événement OrderCreated
3. Consommation de l'événement OrderCreated par le NotificationService pour simuler l'envoi d'un email
4. Routage centralisé via l'API Gateway Ocelot
5. Résilience dans les appels entre services avec Polly

## Installation et Démarrage

### Prérequis

- Docker et Docker Compose
- .NET 8 SDK (pour le développement)

### Démarrage avec Docker Compose

```bash
docker-compose up -d
```

## Endpoints API

Les endpoints détaillés sont disponibles via Swagger une fois les services démarrés :

- API Gateway: http://localhost:8080/swagger
- ProductService: http://localhost:5001/swagger
- OrderService: http://localhost:5002/swagger
- NotificationService: http://localhost:5003/swagger

## Structure du Projet

Le projet est organisé comme suit :

### Fichiers Racine

- `AtelierMicroserviceDotNet.sln` - Fichier solution Visual Studio qui regroupe tous les projets
- `README.md` - Documentation du projet (ce fichier)
- `docker-compose.yml` - Configuration Docker Compose pour orchestrer tous les services
- `.dockerignore` - Spécifie les fichiers à ignorer lors de la construction des images Docker
- `.gitignore` - Spécifie les fichiers à ignorer dans le contrôle de version Git
- `LICENSE` - Licence du projet (MIT)

### Répertoire `/src`

Contient le code source de tous les microservices et de l'API Gateway.

#### `/src/ProductService`

Service responsable de la gestion des produits.

- **Fichiers Principaux**:
  - `ProductService.csproj` - Fichier projet .NET
  - `Program.cs` - Point d'entrée de l'application et configuration des endpoints API
  - `Dockerfile` - Instructions pour construire l'image Docker du service

- **Sous-répertoires**:
  - `/Models` - Définitions des modèles de données
    - `Product.cs` - Classe représentant un produit
  - `/Repositories` - Implémentation du pattern Repository
    - `IProductRepository.cs` - Interface définissant les opérations sur les produits
    - `ProductRepository.cs` - Implémentation en mémoire du repository
  - `/Properties` - Configuration de lancement
    - `launchSettings.json` - Configuration des profils de lancement

- **Fichiers de Configuration**:
  - `appsettings.json` - Configuration générale de l'application
  - `appsettings.Development.json` - Configuration spécifique à l'environnement de développement
  - `ProductService.http` - Fichiers de requêtes HTTP pour tester l'API

#### `/src/OrderService`

Service responsable de la gestion des commandes et de la publication d'événements.

- **Fichiers Principaux**:
  - `OrderService.csproj` - Fichier projet .NET
  - `Program.cs` - Point d'entrée de l'application et configuration des endpoints API
  - `Dockerfile` - Instructions pour construire l'image Docker du service

- **Sous-répertoires**:
  - `/Models` - Définitions des modèles de données
    - `Order.cs` - Classe représentant une commande et ses éléments
  - `/Repositories` - Implémentation du pattern Repository
    - `IOrderRepository.cs` - Interface définissant les opérations sur les commandes
    - `OrderRepository.cs` - Implémentation en mémoire du repository
  - `/Messages` - Définitions des messages pour la communication événementielle
    - `OrderCreated.cs` - Message publié lorsqu'une commande est créée
  - `/Properties` - Configuration de lancement
    - `launchSettings.json` - Configuration des profils de lancement

- **Fichiers de Configuration**:
  - `appsettings.json` - Configuration générale de l'application
  - `appsettings.Development.json` - Configuration spécifique à l'environnement de développement
  - `OrderService.http` - Fichiers de requêtes HTTP pour tester l'API

#### `/src/NotificationService`

Service responsable de la consommation des événements et de l'envoi de notifications.

- **Fichiers Principaux**:
  - `NotificationService.csproj` - Fichier projet .NET
  - `Program.cs` - Point d'entrée de l'application et configuration des endpoints API
  - `Dockerfile` - Instructions pour construire l'image Docker du service

- **Sous-répertoires**:
  - `/Consumers` - Consommateurs d'événements
    - `OrderCreatedConsumer.cs` - Consommateur pour les événements OrderCreated
  - `/Messages` - Définitions des messages pour la communication événementielle
    - `OrderCreated.cs` - Message consommé lorsqu'une commande est créée
  - `/Services` - Services métier
    - `IEmailService.cs` - Interface pour le service d'envoi d'emails
    - `EmailService.cs` - Implémentation du service d'envoi d'emails
  - `/Properties` - Configuration de lancement
    - `launchSettings.json` - Configuration des profils de lancement

- **Fichiers de Configuration**:
  - `appsettings.json` - Configuration générale de l'application
  - `appsettings.Development.json` - Configuration spécifique à l'environnement de développement
  - `NotificationService.http` - Fichiers de requêtes HTTP pour tester l'API

#### `/src/ApiGateway`

API Gateway qui centralise les requêtes vers les différents microservices.

- **Fichiers Principaux**:
  - `ApiGateway.csproj` - Fichier projet .NET
  - `Program.cs` - Point d'entrée de l'application et configuration d'Ocelot
  - `Dockerfile` - Instructions pour construire l'image Docker du service

- **Fichiers de Configuration**:
  - `ocelot.json` - Configuration des routes pour rediriger les requêtes vers les microservices
  - `appsettings.json` - Configuration générale de l'application
  - `appsettings.Development.json` - Configuration spécifique à l'environnement de développement
  - `ApiGateway.http` - Fichiers de requêtes HTTP pour tester l'API Gateway
  - `/Properties/launchSettings.json` - Configuration des profils de lancement

### Répertoire `/logs`

Contient les fichiers de logs générés par les différents services.

- `product-service-*.txt` - Logs du ProductService
- `order-service-*.txt` - Logs du OrderService
- `notification-service-*.txt` - Logs du NotificationService
- `api-gateway-*.txt` - Logs de l'API Gateway

### Fichiers Docker

- `docker-compose.yml` - Définit les services, réseaux et volumes pour l'application complète
  - Configure les 4 services (api-gateway, product-service, order-service, notification-service)
  - Configure RabbitMQ pour la messagerie
  - Définit le réseau "microservices-network" pour la communication entre services
  - Configure les volumes pour la persistance des données et des logs

- `.dockerignore` - Spécifie les fichiers à exclure lors de la construction des images Docker

- Dockerfiles individuels dans chaque répertoire de service:
  - `/src/ProductService/Dockerfile`
  - `/src/OrderService/Dockerfile`
  - `/src/NotificationService/Dockerfile`
  - `/src/ApiGateway/Dockerfile`
