# Formation Microservices: Atelier avec .Net 8

Ce projet sert d'atelier à la formation Microservices avec DotNet 8 dispensée par le cabinet Demkada Academy.

## Architecture du Projet

Le projet est composé de 5 composants principaux :

1. **ProductService** - Service permettant de créer et gérer des produits
2. **OrderService** - Service permettant de créer des commandes et publier des événements
3. **NotificationService** - Service consommant les événements pour envoyer des notifications
4. **API Gateway (Ocelot)** - Passerelle API centralisant les requêtes
5. **IdentityServer** - Service d'authentification et d'autorisation basé sur OAuth 2.0 et OpenID Connect

### Diagramme de Flux de Données

```
┌─────────────┐     ┌─────────────────────┐     ┌─────────────────┐
│             │     │                     │     │                 │
│   Client    │────▶│    API Gateway      │◀───▶│  IdentityServer │
│             │     │     (Ocelot)        │     │                 │
└─────────────┘     └─────────┬───────────┘     └─────────────────┘
      HTTP/REST               │                             ▲  OAuth 2.0/OIDC
                              │                             │
                              │ HTTP/REST                   │ JWT Validation
                              │ + JWT                       ▼ 
                 ┌────────────│─────────────────────────────────────────────┐
                 │            │                                             │             
                 ▼            ▼                                             │
┌─────────────────┐    ┌─────────────────────────┐    ┌─────────────────┐   │
│                 │    │      OrderService       │    │                 │   │
│ ProductService  │◀───┤                         │───▶│    RabbitMQ     │   │
│                 │    │  ┌─────────┐ ┌────────┐ │    │  Message Broker │   │
└─────────────────┘    │  │ Commands│ │Queries │ │    └────────┬────────┘   │
      ▲                │  └────┬────┘ └───┬────┘ │            │             │
      │                │       │          │      │            │             │
      │                │       ▼          ▼      │            │ AMQP        │
      │                │  ┌─────────────────┐    │            │ (MassTransit)│ 
      │                │  │     MediatR     │    │            │             │
      │                │  └────────┬────────┘    │            ▼             │
      │                │           │             │    ┌─────────────────┐   │
      │                │      ┌────┴─────┐       │    │                 │   │
      │                │      │  Sagas   │       │    │NotificationSvc  │ ◀─┘
      │                │      └──────────┘       │    │                 │
      │                └─────────────────────────┘    └─────────────────┘
      │                       │
      └───────────────────────┘
           HTTP/REST avec
         Polly (Resilience)
```

#### Explication du Flux de Données

1. **Client → API Gateway** : Les clients communiquent avec le système via l'API Gateway en utilisant le protocole HTTP/REST. L'API Gateway sert de point d'entrée unique pour toutes les requêtes.

2. **Client → API Gateway → IdentityServer** : Pour les requêtes nécessitant une authentification, l'API Gateway vérifie la validité du token JWT auprès d'IdentityServer. Si aucun token n'est fourni ou si le token est invalide, la requête est rejetée avec une erreur 401 (Unauthorized) ou 403 (Forbidden).

3. **API Gateway → Services** : L'API Gateway (Ocelot) route les requêtes vers les microservices appropriés (ProductService ou OrderService) en utilisant HTTP/REST. Les requêtes incluent le token JWT validé, qui est également vérifié par chaque microservice.

4. **OrderService (CQRS)** : À l'intérieur de l'OrderService, le pattern CQRS est implémenté :
   - Les requêtes HTTP entrantes sont transformées en Commands (pour les opérations d'écriture) ou Queries (pour les opérations de lecture)
   - MediatR dispatche ces Commands/Queries vers les Handlers appropriés
   - Les Handlers exécutent la logique métier et interagissent avec le Repository

5. **OrderService (SAGA)** : Pour les opérations qui nécessitent une coordination entre services :
   - Après la création d'une commande, un OrderCreatedEvent est publié
   - La Saga orchestre le processus en suivant les étapes et en maintenant l'état dans OrderSagaState
   - En cas d'échec, des actions compensatoires sont exécutées pour maintenir la cohérence

6. **OrderService → ProductService** : Lors de la création d'une commande, l'OrderService peut avoir besoin de vérifier les informations des produits auprès du ProductService. Cette communication utilise HTTP/REST avec Polly pour la résilience (retry, circuit breaker).

7. **OrderService → RabbitMQ** : Lorsqu'une commande est créée, l'OrderService publie des événements sur RabbitMQ en utilisant le protocole AMQP via MassTransit :
   - `OrderCreatedEvent` pour la Saga interne
   - `OrderCreated` pour la notification externe

8. **RabbitMQ → NotificationService** : Le NotificationService s'abonne aux événements `OrderCreated` sur RabbitMQ et les consomme via AMQP (MassTransit) pour envoyer des notifications par email.

Cette architecture découplée permet une grande flexibilité et résilience. Si un service est temporairement indisponible, les messages restent dans la file d'attente RabbitMQ et seront traités une fois le service rétabli. L'utilisation de CQRS et SAGA renforce cette résilience en permettant une meilleure séparation des préoccupations et une gestion robuste des transactions distribuées.

## Technologies Utilisées

- **.NET 8** avec Minimal API
- **Docker** et **Docker Compose** pour la conteneurisation
- **Ocelot** comme API Gateway
- **Duende IdentityServer** pour l'authentification et l'autorisation OAuth 2.0/OpenID Connect
- **JWT** (JSON Web Tokens) pour la sécurisation des API
- **Polly** pour la résilience
- **MediatR** pour l'implémentation du pattern CQRS
- **MassTransit** avec **RabbitMQ** pour la messagerie et l'orchestration des Sagas
- **Swagger** pour la documentation des API
- **Serilog** pour le logging structuré et centralisé
- **OpenTelemetry** pour l'instrumentation et la collecte de télémétrie
- **Grafana**, **Prometheus**, **Loki** et **Tempo** pour l'observabilité

## Sécurité avec IdentityServer et JWT

Le projet implémente une couche de sécurité robuste basée sur OAuth 2.0 et OpenID Connect avec Duende IdentityServer, garantissant que seules les requêtes authentifiées et autorisées peuvent accéder aux ressources protégées.

### Architecture de Sécurité

- **IdentityServer** agit comme fournisseur d'identité (IdP) centralisé pour l'ensemble du système
- **API Gateway** valide les tokens JWT et applique les politiques d'autorisation basées sur les scopes
- **Microservices** vérifient également la validité des tokens JWT pour une sécurité en profondeur

### Flux d'Authentification

1. Le client obtient un token JWT auprès d'IdentityServer en utilisant le flux "Client Credentials"
2. Le client inclut ce token dans l'en-tête Authorization de ses requêtes vers l'API Gateway
3. L'API Gateway valide le token auprès d'IdentityServer et vérifie les scopes requis
4. Si le token est valide et contient les scopes appropriés, la requête est transmise au microservice concerné
5. Le microservice vérifie également le token avant de traiter la requête

### Obtention d'un Token JWT

Pour obtenir un token JWT, vous pouvez utiliser la commande curl suivante :

```bash
curl -X POST \
  http://localhost:5004/connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=client_credentials&client_id=api-gateway&client_secret=secret&scope=orders:write'
```

La réponse contiendra un token JWT que vous pourrez utiliser pour les requêtes ultérieures :

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsImtpZCI6IkYyNjZCQzA5RkE5NTU3Q0JFRDhEOTg0NEYwRDVGMDVGIiwidHlwIjoiYXQrand0In0...",
  "expires_in": 3600,
  "token_type": "Bearer",
  "scope": "orders:write"
}
```

### Utilisation du Token JWT

Pour utiliser le token JWT dans vos requêtes, ajoutez-le dans l'en-tête Authorization :

```bash
curl -X GET \
  http://localhost:8080/api/orders \
  -H 'Authorization: Bearer eyJhbGciOiJSUzI1NiIsImtpZCI6IkYyNjZCQzA5RkE5NTU3Q0JFRDhEOTg0NEYwRDVGMDVGIiwidHlwIjoiYXQrand0In0...'
```

### Scopes Disponibles

Les scopes suivants sont configurés dans le système :

- `orders:read` - Lecture des commandes
- `orders:write` - Création et modification des commandes
- `products:read` - Lecture des produits
- `products:write` - Création et modification des produits

### Sécurité en Profondeur

Chaque couche du système applique des contrôles de sécurité :

1. **API Gateway** - Première ligne de défense qui valide les tokens et applique les politiques d'autorisation
2. **Microservices** - Seconde ligne de défense qui vérifie également les tokens
3. **Communication directe bloquée** - Les microservices ne sont pas accessibles directement depuis l'extérieur, toutes les requêtes doivent passer par l'API Gateway

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
- Kubernetes (Docker Desktop avec Kubernetes activé ou Minikube)
- kubectl (outil en ligne de commande Kubernetes)

### Démarrage avec Docker Compose

```bash
docker-compose up -d
```

### Démarrage avec Kubernetes

Le projet inclut des descripteurs Kubernetes pour déployer l'application dans un cluster Kubernetes.

#### Architecture Kubernetes

L'architecture Kubernetes suit la même structure que la version Docker Compose, mais avec quelques différences importantes :

1. **Namespace dédié** : Tous les composants sont déployés dans un namespace `microservices` dédié pour une meilleure isolation.

2. **Services Kubernetes** : Chaque composant est exposé via un Service Kubernetes :
   - Les services internes sont de type ClusterIP (accessibles uniquement à l'intérieur du cluster)
   - L'API Gateway est exposé via un service de type LoadBalancer (accessible depuis l'extérieur)
   - Grafana est également accessible depuis l'extérieur pour la visualisation

3. **ConfigMaps** : Les configurations sont stockées dans des ConfigMaps Kubernetes :
   - Configuration RabbitMQ
   - Configuration Grafana
   - Configuration des outils d'observabilité (Prometheus, Loki, Tempo, OpenTelemetry)

4. **Volumes** : Des volumes emptyDir sont utilisés pour la persistance des données (dans un environnement de production, vous devriez utiliser des PersistentVolumes)

5. **Probes de santé** : Chaque déploiement inclut des probes de santé (liveness et readiness) pour une meilleure résilience

6. **Ressources** : Des limites et requêtes de ressources sont définies pour chaque conteneur

7. **Kustomize** : Un fichier kustomization.yaml est fourni pour faciliter le déploiement de tous les composants

#### Étape 1: Construire les images Docker

Avant de déployer sur Kubernetes, vous devez construire les images Docker localement :

```bash
docker-compose build
```

#### Étape 2: Déployer sur Kubernetes

Utilisez kubectl avec kustomize pour déployer tous les composants :

```bash
kubectl apply -k kubernetes/
```

Cette commande déploiera :
- Le namespace `microservices`
- Les ConfigMaps pour la configuration
- Les composants d'infrastructure (RabbitMQ, Prometheus, Loki, Tempo, OpenTelemetry Collector, Grafana)
- Les microservices (IdentityServer, ApiGateway, ProductService, OrderService, NotificationService)

#### Étape 3: Vérifier le déploiement

Vérifiez que tous les pods sont en cours d'exécution :

```bash
kubectl get pods -n microservices
```

#### Étape 4: Accéder aux services

L'API Gateway est exposée via un service de type LoadBalancer sur le port 8080 :

```bash
kubectl get service api-gateway-service -n microservices
```

Vous pouvez accéder à l'API Gateway à l'adresse http://localhost:8080

Grafana est accessible à l'adresse http://localhost:3000 (utilisateur: admin, mot de passe: admin)

#### Étape 5: Supprimer le déploiement

Pour supprimer tous les composants déployés :

```bash
kubectl delete -k kubernetes/
```

## Observabilité

Le projet intègre une stack complète d'observabilité pour surveiller, déboguer et optimiser les microservices en production.

### Architecture d'Observabilité

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Microservices  │     │ OpenTelemetry   │     │    Backends     │
│                 │     │   Collector     │     │                 │
│  ┌───────────┐  │     │                 │     │  ┌───────────┐  │
│  │ Serilog   │  │     │                 │     │  │   Loki    │  │
│  │ with OTLP │──┼────▶│                 │────▶│  │           │  │
│  │  Sink     │  │     │                 │     │  └───────────┘  │
│  └───────────┘  │     │                 │     │                 │
│                 │     │                 │     │  ┌───────────┐  │
│  ┌───────────┐  │     │                 │     │  │Prometheus │  │
│  │ OpenTel   │──┼────▶│                 │────▶│  │           │  │
│  │ Metrics   │  │     │                 │     │  └───────────┘  │
│  └───────────┘  │     │                 │     │                 │
│                 │     │                 │     │  ┌───────────┐  │
│  ┌───────────┐  │     │                 │     │  │  Tempo    │  │
│  │ OpenTel   │──┼────▶│                 │────▶│  │           │  │
│  │ Traces    │  │     │                 │     │  └───────────┘  │
│  └───────────┘  │     │                 │     │                 │
└─────────────────┘     └─────────────────┘     └────────┬────────┘
                                                         │
                                                         ▼
                                                ┌─────────────────┐
                                                │    Grafana      │
                                                │  ┌───────────┐  │
                                                │  │ Dashboards│  │
                                                │  └───────────┘  │
                                                └─────────────────┘
```

### Composants d'Observabilité

1. **Collecte de Logs**
   - Serilog est configuré avec le sink OpenTelemetry pour envoyer des logs structurés au collecteur OpenTelemetry
   - Le collecteur OpenTelemetry transmet ensuite les logs à Loki
   - Les logs incluent des métadonnées comme le service, l'environnement et le niveau de sévérité

2. **Métriques**
   - OpenTelemetry collecte des métriques techniques (CPU, mémoire, latence HTTP)
   - Les métriques sont exportées uniquement vers le collecteur OpenTelemetry
   - Le collecteur OpenTelemetry transmet ensuite les métriques à Prometheus

3. **Traces Distribuées**
   - OpenTelemetry trace les requêtes à travers les différents microservices
   - Les traces sont exportées vers le collecteur OpenTelemetry
   - Le collecteur OpenTelemetry transmet ensuite les traces à Tempo
   - Intégration avec MassTransit pour tracer les messages asynchrones

4. **Visualisation**
   - Grafana fournit des dashboards pour visualiser logs, métriques et traces
   - Corrélation entre les différentes sources de données
   - Alertes configurables basées sur les métriques et logs

### Accès aux Interfaces

- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **Loki**: http://localhost:3100
- **Tempo**: http://localhost:3200

### Comment Tester et Visualiser l'Observabilité

Pour tester et visualiser les métriques, traces et logs dans Grafana, suivez ces étapes :

#### Génération de Données d'Observabilité

1. **Générer des Métriques et Traces** :
   ```bash
   # Créer un produit (génère des métriques et traces)
   curl -X POST http://localhost:5001/api/products \
     -H "Content-Type: application/json" \
     -d '{"name":"Produit Test","description":"Description du produit","price":19.99,"stockQuantity":100}'

   # Obtenir la liste des produits (génère des métriques et traces)
   curl http://localhost:5001/api/products
   ```

2. **Générer des Logs** :
   ```bash
   # Créer une commande (génère des logs dans OrderService)
   curl -X POST http://localhost:5002/api/orders \
     -H "Content-Type: application/json" \
     -d '{"customerId":"00000000-0000-0000-0000-000000000001","items":[{"productId":"PRODUCT_ID_HERE","quantity":2,"price":19.99}]}'
   ```

#### Visualisation dans Grafana

1. **Accéder à Grafana** :
   - Ouvrez votre navigateur et accédez à http://localhost:3000
   - Connectez-vous avec les identifiants par défaut (admin/admin)

2. **Visualiser les Métriques** :
   - Dans le menu latéral, cliquez sur "Dashboards" > "Browse"
   - Sélectionnez le dossier "Microservices"
   - Ouvrez le dashboard "Microservices Dashboard"
   - Consultez les panneaux "HTTP Request Duration" et "HTTP Request Rate" pour voir les métriques de performance

3. **Visualiser les Logs** :
   - Dans le même dashboard, faites défiler jusqu'au panneau "Logs"
   - Vous pouvez filtrer les logs par service en utilisant la requête : `{service="product-service"}` ou `{service="order-service"}`
   - Pour rechercher des logs spécifiques, ajoutez des termes de recherche : `{service="order-service"} |= "commande créée"`

4. **Visualiser les Traces** :
   - Dans le même dashboard, faites défiler jusqu'au panneau "Traces"
   - Cliquez sur une trace pour voir le détail des spans
   - Vous pouvez voir la durée de chaque opération et les relations entre les services

5. **Explorer les Données Brutes** :
   - Dans le menu latéral, cliquez sur "Explore"
   - Sélectionnez la source de données "Prometheus" pour explorer les métriques
   - Sélectionnez "Loki" pour explorer les logs
   - Sélectionnez "Tempo" pour explorer les traces

6. **Corrélation entre Logs et Traces** :
   - Dans une vue de logs, cliquez sur un log contenant un ID de trace
   - Grafana vous permettra de naviguer directement vers la trace correspondante
   - Inversement, dans une vue de trace, vous pouvez accéder aux logs associés

#### Requêtes Utiles

1. **Métriques Prometheus** :
   - Taux de requêtes HTTP : `rate(http_server_duration_count[5m])`
   - Durée moyenne des requêtes : `rate(http_server_duration_sum[5m]) / rate(http_server_duration_count[5m])`
   - Utilisation CPU : `process_cpu_seconds_total`
   - Toutes les métriques sont collectées par le collecteur OpenTelemetry et exportées vers Prometheus

2. **Requêtes Loki** :
   - Tous les logs d'erreur : `{job=~".+"} |= "error" | logfmt`
   - Logs par service : `{service="order-service"}`
   - Logs avec durée élevée : `{job=~".+"} |= "duration" | duration > 500ms`
   - Tous les logs sont envoyés via Serilog avec le sink OpenTelemetry au collecteur, puis transmis à Loki

3. **Requêtes Tempo** :
   - Traces par service : `service.name="order-service"`
   - Traces avec erreurs : `status.code=ERROR`
   - Traces longues : `duration > 100ms`
   - Toutes les traces sont collectées par OpenTelemetry, envoyées au collecteur, puis transmises à Tempo

## Endpoints API

Les endpoints détaillés sont disponibles via Swagger une fois les services démarrés :

- API Gateway: http://localhost:8080/swagger
- ProductService: http://localhost:5001/swagger
- OrderService: http://localhost:5002/swagger
- NotificationService: http://localhost:5003/swagger
- IdentityServer: http://localhost:5004/.well-known/openid-configuration (Découverte OpenID Connect)

### Endpoints IdentityServer

IdentityServer expose plusieurs endpoints standards OAuth 2.0 et OpenID Connect :

- **Discovery Document**: `http://localhost:5004/.well-known/openid-configuration`
- **Token Endpoint**: `http://localhost:5004/connect/token` (pour obtenir un token JWT)
- **Introspection Endpoint**: `http://localhost:5004/connect/introspect` (pour valider un token)
- **Health Check**: `http://localhost:5004/health`

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

Contient le code source de tous les microservices, de l'API Gateway et d'IdentityServer.

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

#### `/src/IdentityServer`

Service d'authentification et d'autorisation basé sur OAuth 2.0 et OpenID Connect.

- **Fichiers Principaux**:
  - `IdentityServer.csproj` - Fichier projet .NET
  - `Program.cs` - Point d'entrée de l'application et configuration d'IdentityServer
  - `Dockerfile` - Instructions pour construire l'image Docker du service

- **Configuration**:
  - Configuration des clients, des ressources API, des scopes et des utilisateurs de test
  - Génération de clés de signature pour les tokens JWT
  - Exposition des endpoints OAuth 2.0 et OpenID Connect standards

- **Fichiers de Configuration**:
  - `appsettings.json` - Configuration générale de l'application
  - `appsettings.Development.json` - Configuration spécifique à l'environnement de développement
  - `/Properties/launchSettings.json` - Configuration des profils de lancement

### Répertoire `/logs`

Contient les fichiers de logs générés par les différents services.

- `product-service-*.txt` - Logs du ProductService
- `order-service-*.txt` - Logs du OrderService
- `notification-service-*.txt` - Logs du NotificationService
- `api-gateway-*.txt` - Logs de l'API Gateway
- `identity-server-*.txt` - Logs d'IdentityServer

### Fichiers Docker

- `docker-compose.yml` - Définit les services, réseaux et volumes pour l'application complète
  - Configure les 5 services (identity-server, api-gateway, product-service, order-service, notification-service)
  - Configure RabbitMQ pour la messagerie
  - Définit le réseau "microservices-network" pour la communication entre services
  - Configure les volumes pour la persistance des données et des logs

- `.dockerignore` - Spécifie les fichiers à exclure lors de la construction des images Docker

- Dockerfiles individuels dans chaque répertoire de service:
  - `/src/IdentityServer/Dockerfile`
  - `/src/ApiGateway/Dockerfile`
  - `/src/ProductService/Dockerfile`
  - `/src/OrderService/Dockerfile`
  - `/src/NotificationService/Dockerfile`
