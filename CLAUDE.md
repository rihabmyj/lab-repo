# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Contexte

Application e-commerce .NET 10 / ASP.NET Core, orchestrée par .NET Aspire (SDK `Aspire.AppHost.Sdk/13.4.3`). C'est l'application fil rouge d'une formation « Claude Code pour les Dev & DevOps » : elle est livrée **volontairement nue** (pas de tests, pas de base réelle, pas de Dockerfile, pas de CI). Ces éléments sont censés être ajoutés au fil des labs — ne pas supposer qu'ils existent déjà.

## Commandes

Prérequis : SDK .NET 10 uniquement (Docker inutile). Une fois par poste : `dotnet dev-certs https --trust` (sinon erreurs `UntrustedRoot` sur le dashboard Aspire).

```bash
dotnet build ECommerce.slnx                 # compiler toute la solution
dotnet run --project src/ECommerce.AppHost  # lancer TOUS les services + dashboard Aspire
```

- Lancer l'AppHost plutôt que les services individuellement : sans lui, rien n'injecte les adresses des dépendances. Un service lancé seul démarre (chacun a un profil dans `Properties/launchSettings.json`) mais échoue dès qu'il appelle un autre service ; Catalog, qui n'en appelle aucun, fonctionne seul.
- Ouvrir la `Login URL` affichée par le terminal. Le dashboard est sur un port fixe (`https://localhost:17089`, voir `src/ECommerce.AppHost/Properties/launchSettings.json`) et la passerelle répond sur `http://localhost:5067` (utilisé par `smoke.sh`). Les ports des autres services sont attribués par Aspire et changent à chaque lancement : les lire dans le dashboard. Ne pas ouvrir l'URL `OTLP/gRPC`.
- Aucun projet de test ni linter n'est configuré pour l'instant. Une fois un projet de test ajouté à la solution : `dotnet test ECommerce.slnx`, et pour un seul test `dotnet test --filter "FullyQualifiedName~NomDuTest"`.
- OpenAPI exposé en Development sur `/openapi/v1.json` de chaque API.

## Architecture

```
Web (Blazor Server) ──HTTP──> Gateway (YARP) ──/catalog/*──> Catalog.Api
                                              └─/ordering/*─> Ordering.Api ──HTTP──> Catalog.Api
```

- **`src/ECommerce.AppHost/AppHost.cs`** est la source de vérité de la topologie : noms des ressources (`catalog`, `ordering`, `gateway`, `web`), références (`WithReference`) et ordre de démarrage (`WaitFor`). Seul `web` a des endpoints externes.
- **Découverte de services** : les services ne connaissent jamais d'adresse en dur. Les clients HTTP utilisent `https+http://<nom-ressource>` (HTTPS préféré, repli HTTP), résolu à l'exécution à partir des variables `services__<nom>__https__0` injectées par Aspire grâce à `WithReference`. Ajouter un appel inter-services = ajouter le `WithReference` correspondant dans l'AppHost **et** utiliser le nom de ressource comme hôte.
- **`ECommerce.ServiceDefaults/Extensions.cs`** : chaque service appelle `builder.AddServiceDefaults()` et `app.MapDefaultEndpoints()`. Cela active OpenTelemetry, la découverte de services et `AddStandardResilienceHandler()` sur **tous** les `HttpClient`. Les endpoints `/health` et `/alive` ne sont mappés qu'en Development.
- **Gateway** : routage entièrement déclaratif dans `src/ECommerce.Gateway/appsettings.json` (section `ReverseProxy`). Le préfixe `/catalog` ou `/ordering` est retiré avant transfert, donc les APIs exposent `/api/products` et `/api/orders` sans préfixe. Le Web appelle toujours via la gateway (`/catalog/api/...`, `/ordering/api/...`), jamais les APIs directement.
- **APIs** : Minimal APIs regroupées dans `Endpoints/*Endpoints.cs` (`MapGroup`), EF Core avec `UseInMemoryDatabase` + `EnsureCreated()` au démarrage (pas de migrations). Les données de démo du catalogue sont dans `CatalogDbContext.OnModelCreating` (`HasData`). Les données disparaissent à chaque redémarrage.
- **Création de commande** (`OrderingEndpoints.cs`) : pour chaque ligne, Ordering appelle Catalog (`CatalogServiceClient`, `GET /api/products/{id}`) ; produit absent → 400. Nom et prix sont copiés depuis Catalog. La requête est d'abord validée sans appeler Catalog (client non vide de 200 caractères au plus, au moins une ligne, quantités > 0), puis la somme des quantités par produit est comparée à `AvailableStock`. Tous les refus sont des 400 avec un message texte. Le stock est vérifié mais ni réservé ni décrémenté : deux commandes successives peuvent dépasser le stock total.
- **DTOs dupliqués** : il n'y a pas de projet de contrats partagé. Les records côté Web (`Services/*ApiClient.cs`) et côté Ordering (`CatalogProduct`) dupliquent les modèles des APIs — les garder synchronisés en cas de changement de modèle. Les enums sont sérialisés en chaînes dans Ordering (`JsonStringEnumConverter`).
- **Web** : Blazor en rendu `InteractiveServer` (le code des pages s'exécute côté serveur). Le panier est un état en mémoire du composant `Products.razor`.

## Points d'attention du dépôt

- Il n'y a **pas de `.gitignore`** et les dossiers `bin/` et `obj/` sont versionnés par erreur. Ne pas committer de nouveaux artefacts de build.
- Les commentaires de code et les README de chaque projet sont en anglais ; seul le `README.md` racine est en français.
