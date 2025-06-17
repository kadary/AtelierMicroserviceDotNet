# Atelier Microservices DotNet 8

Ce projet sert d'atelier à la formation Microservices avec DotNet 8 dispensée par le cabinet Demkada Academy.

## Architecture du Projet

Le projet est composé de 4 composants principaux :

1. **ProductService** - Service permettant de créer et gérer des produits
2. **OrderService** - Service permettant de créer des commandes et publier des événements
3. **NotificationService** - Service consommant les événements pour envoyer des notifications
4. **API Gateway (Ocelot)** - Passerelle API centralisant les requêtes

### Diagramme de Flux de Données

```
┌─────────────┐     ┌─────────────┐     ┌─────────────────┐
│             │     │             │     │                 │
│   Client    │────▶│ API Gateway │────▶│ ProductService  │
│             │     │  (Ocelot)   │     │                 │
└─────────────┘     └──────┬──────┘     └─────────────────┘
                           │
                           │            ┌─────────────────┐
                           │            │                 │
                           └───────────▶│  OrderService   │
                                        │                 │
                                        └────────┬────────┘
                                                 │
                                                 │ (MassTransit + RabbitMQ)
                                                 │
                                        ┌────────▼────────┐
                                        │                 │
                                        │NotificationSvc  │
                                        │                 │
                                        └─────────────────┘
```

## Technologies Utilisées

- **.NET 8** avec Minimal API
- **Docker** et **Docker Compose** pour la conteneurisation
- **Ocelot** comme API Gateway
- **Polly** pour la résilience
- **MassTransit** avec **RabbitMQ** pour la messagerie
- **Swagger** pour la documentation des API
- **Serilog** pour le logging

## Patterns de Conception Utilisés

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

- API Gateway: http://localhost:5000/swagger
- ProductService: http://localhost:5001/swagger
- OrderService: http://localhost:5002/swagger
- NotificationService: http://localhost:5003/swagger

## Structure du Projet

Le projet est organisé comme suit :

- `/src` - Code source des services
  - `/ProductService` - Service de gestion des produits
  - `/OrderService` - Service de gestion des commandes
  - `/NotificationService` - Service de notification
  - `/ApiGateway` - API Gateway avec Ocelot
- `/docker` - Fichiers Docker et Docker Compose
