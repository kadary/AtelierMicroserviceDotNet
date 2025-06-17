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