# Objectif

Remplacer les fichiers Excel/VBA par une application **portable** (clé USB) facile à maintenir, couvrant : devis/factures/clients/articles/stock/caisse légère, flotte radio 4G/POC/OTA/abonnements/SIM, et gestion UHF (plans de fréquences, programmation, historique).

---

## Stack recommandée

- **.NET 8 (C#) + WPF** (UI desktop Windows)
- **SQLite** (fichier `.db` local, portable)
- Bibliothèques proposées :
  - Données : `Microsoft.Data.Sqlite`, **(au choix)** `Dapper` *ou* `EF Core`
  - PDF : `PdfSharp + MigraDoc`
  - Série/USB : `System.IO.Ports`
  - CSV : `CsvHelper`
  - Codes-barres (si besoin décoder images) : `ZXing.Net`
  - MVVM : `CommunityToolkit.Mvvm`

> Cible : Windows 64-bit. Exécutable **self-contained** (pas d’installation), copiable sur clé USB.

---

## Structure de solution (propre & maintenable)

```
RadioSuite.sln
  ├─ RadioSuite.UI.Wpf           (UI WPF, MVVM, vues)
  ├─ RadioSuite.Core             (logique métier : devis, stocks, UHF, règles)
  ├─ RadioSuite.Data             (DAL, SQLite, migrations)
  ├─ RadioSuite.Serial           (ports COM, protocoles radios UHF/4G)
  ├─ RadioSuite.Reports          (PDF, exports)
  ├─ RadioSuite.ImportExport     (CSV/XLSX mappings)
  └─ RadioSuite.Tests            (tests unitaires)
```

- UI ne connaît que **Core** ; **Core** utilise **Data/Serial/Reports/ImportExport** via interfaces.
- Inversion de dépendances (DI) avec `Microsoft.Extensions.DependencyInjection`.

---

## Création du projet (pas à pas)

1. **Installer .NET SDK 8** (une seule fois sur ta machine de dev).
2. Dans un dossier de travail :

```bash
dotnet new sln -n RadioSuite
mkdir RadioSuite.UI.Wpf RadioSuite.Core RadioSuite.Data RadioSuite.Serial RadioSuite.Reports RadioSuite.ImportExport RadioSuite.Tests
cd RadioSuite.UI.Wpf

dotnet new wpf -n RadioSuite.UI.Wpf
cd ..
dotnet new classlib -n RadioSuite.Core
dotnet new classlib -n RadioSuite.Data
dotnet new classlib -n RadioSuite.Serial
dotnet new classlib -n RadioSuite.Reports
dotnet new classlib -n RadioSuite.ImportExport

dotnet new mstest -n RadioSuite.Tests

# Ajouter projets à la solution
cd ..
dotnet sln RadioSuite.sln add RadioSuite.UI.Wpf/RadioSuite.UI.Wpf.csproj \
  RadioSuite.Core/RadioSuite.Core.csproj \
  RadioSuite.Data/RadioSuite.Data.csproj \
  RadioSuite.Serial/RadioSuite.Serial.csproj \
  RadioSuite.Reports/RadioSuite.Reports.csproj \
  RadioSuite.ImportExport/RadioSuite.ImportExport.csproj \
  RadioSuite.Tests/RadioSuite.Tests.csproj

# Références
cd RadioSuite.UI.Wpf
 dotnet add reference ../RadioSuite.Core/RadioSuite.Core.csproj \
                     ../RadioSuite.Reports/RadioSuite.Reports.csproj \
                     ../RadioSuite.ImportExport/RadioSuite.ImportExport.csproj
cd ../RadioSuite.Core
 dotnet add reference ../RadioSuite.Data/RadioSuite.Data.csproj \
                     ../RadioSuite.Serial/RadioSuite.Serial.csproj
```

3. **Packages NuGet** (ajouter où pertinent) :

```bash
# Data
cd RadioSuite.Data
 dotnet add package Microsoft.Data.Sqlite
 # Choisir un ORM: Dapper (léger) ou EF Core (routé)
 dotnet add package Dapper
 # ou
 # dotnet add package Microsoft.EntityFrameworkCore.Sqlite
 # dotnet add package Microsoft.EntityFrameworkCore.Design

# Import/Export
cd ../RadioSuite.ImportExport
 dotnet add package CsvHelper

# Reports (PDF)
cd ../RadioSuite.Reports
 dotnet add package PdfSharp-MigraDoc-GDI

# Série/COM
cd ../RadioSuite.Serial
 dotnet add package System.IO.Ports

# MVVM & DI
cd ../RadioSuite.UI.Wpf
 dotnet add package CommunityToolkit.Mvvm
 dotnet add package Microsoft.Extensions.DependencyInjection
```

---

## Publication **portable** (clé USB)

Dans `RadioSuite.UI.Wpf.csproj`, ajouter :

```xml
<PropertyGroup>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <Prefer32Bit>false</Prefer32Bit>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

Commandes de build :

```bash
# Build release self-contained en un seul fichier
cd RadioSuite.UI.Wpf
 dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o ./publish
```

Copier sur la clé USB :

```
/RadioSuite/
  RadioSuite.UI.Wpf.exe
  /Data/     (app.db)
  /Backups/
  /Logs/
  /Config/   (appsettings.json)
  /Drivers/  (optionnel)
```

> **Sans installation** : tu lances l’EXE depuis la clé. Prévoir :
>
> - **SmartScreen** peut alerter (non-signé). Solution : certificat de signature de code ou whitelist interne.
> - Politiques d’entreprise : certains PC bloquent l’exécution depuis USB.
> - Périphériques : les lecteurs code-barres (HID clavier) marchent out-of-the-box; les adaptateurs USB-série nécessitent leurs drivers (souvent déjà présents).

---

## Config & chemins **relatifs**

- `AppDataRoot` = dossier de l’EXE (`AppContext.BaseDirectory`).
- `Data\app.db`, `Backups\`, `Logs\`, `Config\appsettings.json`.
- Logger : `Serilog` (rolling files dans `Logs`).

---

## Schéma SQLite (ébauche)

- **Clients**(Id, RaisonSociale, TVA, Adresse, Contacts, …)
- **Articles**(Id, Ref, Libelle, PrixHT, TVA, CodeBarres, …)
- **Stock**(Id, ArticleId, Depot, Qte, Mini)
- **MouvementsStock**(Id, ArticleId, Type, Qte, Date, RefDoc)
- **Devis**(Id, Numero, ClientId, Date, Etat, TotalHT, TotalTVA, TotalTTC)
- **DevisLignes**(Id, DevisId, ArticleId, Qte, PrixHT, Remise)
- **Factures** / **FacturesLignes** (idem)
- **CaisseEcritures**(Id, Date, Mode, Montant, Ref)
- **Appareils4G**(Id, Modele, IMEI, ICCID, IMSI, CompteOTA, AboId, Statut)
- **Abonnements4G**(Id, Operateur, Forfait, Echeance, Cout, Renouvellement)
- **RadiosUHF**(Id, Modele, NumeroSerie, ProfilId, DerniereProg)
- **Frequences**(Id, Bande, Canal, Pas, ValeurMHz, Etat)
- **AttributionsFreq**(Id, FrequenceId, RadioId, DateDebut, DateFin, Commentaire)
- **Historique**(Id, Entite, Cle, Action, Horodatage, User)

---

## Import CSV (v1)

- Écran de mappage colonnes → champs (profils d’import sauvegardés).
- Prévisualisation, validation, rapport d’erreurs.
- Modes : **Créer**, **Mettre à jour**, **Ignorer**.

---

## UHF & ports COM (v1)

- Détection ports disponibles, paramètres (baud/parité/stop/timeout).
- Abstraction `IRadioDriver` (un driver par modèle si protocole différent).
- Scripts de programmation (écriture profil/canaux), lecture configuration, log hex.

---

## Stratégie de migration depuis Excel

1. **Geler** les fichiers Excel (copie).
2. **Exporter** onglets clefs en **CSV**.
3. **Mapper** les colonnes → tables ci-dessus.
4. **Importer** dans SQLite + contrôles d’intégrité.
5. **Reprendre** les numérotations (devis/factures) et paramètres TVA.
6. **Valider** périphériques (lecteur code-barres, COM UHF).

---

## Roadmap (proposée)

- **M1**: Base projet + schéma SQLite + CRUD Clients/Articles/Stock + import CSV + PDF devis.
- **M2**: Devis→Factures + export compta + journal caisse simple.
- **M3**: Flotte 4G/POC/OTA + SIM + recherche par code-barres.
- **M4**: UHF v1 (lecture/écriture simple) + gestion fréquences/attributions.
- **M5**: sauvegardes auto, droits utilisateurs, optimisations UI.

---

## Bonnes pratiques de maintenance

- **Git** (GitHub/GitLab) : issues pour idées/bugs, tags `feature/bug`.
- **Versionning** : `MAJEUR.MINEUR.CORRECTIF`.
- **Migrations DB** : scripts SQL versionnés (ou EF Core Migrations si EF est choisi).
- **Logs** : garder 30 jours dans `Logs`.
- **Backups** : rotation quotidienne/hebdo dans `Backups`.

---

## À fournir pour démarrer

- Les **2 Excel** + (si possible) une **liste des onglets** et **macro VBA** principales.
- Si tu as des docs des **protocoles UHF** ou formats d’export/import des radios.

---

## Notes PC d’entreprise

- Possible blocage par **SmartScreen**/**antivirus** (appli non signée). Solution : signature de code ou validation IT.
- Accès COM/USB parfois restreint : prévoir un mode « simulation » pour tests.

---

## Exigences ajoutées (benchmark du 13/09/2025)

### Décisions (dernier lot)

- **Marges en direct** : **masquées par défaut** à l’écran, avec un **petit bouton “œil”** pour **afficher/masquer** au besoin. Jamais imprimées.
- **Remise globale** : case à cocher **“Remise (EUR)”** sous le tableau ; champ **montant en €**. **Ligne cachée** si remise = 0.
- **Packs / Kits** : article composé (liste de composants avec quantités) → décrémente **stock des composants** à la vente.
- **Numérotation multi‑séries** : séries distinctes par **type** (DEV, FAC, AVOIR…) et **année** (ex. `DEV-2025-0001`). Paramétrable dans Réglages.
- **Modèles de devis** : **non** (un modèle simple), annexes ajoutées à la demande.
- **Relances devis non transformés** : **non**.
- **Bons de commande / BL** : **non**. Ajout de champs **Transporteur** + **N° de suivi** (texte libres) sur devis/factures.
- **Variantes avancées** : **oui** (attributs libres) avec **SKU par variante**.
- **Seuil mini** : **alerte** quand stock < mini (pas de max ni d’inventaire tournant).
- **Étiquettes code‑barres** : **oui** (A4 PDF + futur support étiqueteuse).
- **Historique prix d’achat** : **oui**.
- **Import tarifs fournisseurs (CSV)** : **oui** (mapping + prévisu).
- **États de vie (parc)** : **oui** (**en stock** → **vendue (chez client)** → **SAV** → **retirée/perdue**), avec historique. **Pas de statut “prêt”** (tu ne loues pas les radios).
- **Pool de rechange** : **non**.
- **RMA / SAV** : **plus tard** (non v1).
- **Plan de fréquences (UHF)** : **oui** + **tolérance** : **même fréquence autorisée pour un même client** si sites **auto** suffisamment **distants** (on stocke le site/événement et la zone).
- **CHIRP+ profils** : **oui** (gabarit commun + export par modèle), exemples à venir.
- **Checklists d’expédition** : **non**.
- **Relances internes SIM** : **oui**, horizon **J‑15**, règles :
  - **SIM active sans client** : **pas d’alerte** si **statut = en stock** ; **alerte** si **déployée** et sans client.
  - **Radio déployée sans SIM** : **alerte**.
  - **Contrat sans activation** et **MoisActifsPlanifiés** risquent d’**excéder** les mois restants → **alerte J‑15** avant dépassement.
- **Destinataires prestataires** : **oui** (contacts par action, CC/BCC auto, modèles d’emails dédiés Activation/Suspension).
- **MRR‑like** (coûts récurrents vs CA) : **oui**.
- **Fin de contrats (pipeline)** : **à confirmer** (proposé : vue par mois/client + charge emails à venir).
- **Rentabilité** : **oui** par **client** **et** par **produit** (vue annuelle incluse).
- **What‑if acompte** : **oui** (simulateur selon coûts/URSSAF).
- **Sauvegardes auto** : **oui** + **sauvegarde externe** (options : **SFTP/WebDAV/dossier réseau**). Zip **chiffré** (mot de passe) avec rotation.
- **Pack support** : **oui** (export diagnostics : logs + config + info système non sensible).
- **Rôles utilisateurs** : **oui** (Admin / Opérateur / Lecture seule) ; accès aux secrets POC réservé Admin.
- **Audit** : **oui** (affichage PW POC, envois prestataire, suppressions, imports, changements de fréquences…).
- **Mode sombre & raccourcis** : **oui** ; compatibilité **Tera D5100** (HID clavier) + sons de confirmation scan.
- **Mode simulation (bac à sable)** : **oui** (option **“Démarrer en mode test”**) :
  - Base **temporaire** séparée (ou en mémoire) → **aucune donnée persistée** dans la base principale.
  - **Aucun envoi réel** (emails bloqués en Outbox test, ports COM simulés).
  - Indication visuelle **“SIMULATION”** partout.

### Statut de paiement des factures

- **Deux statuts** : `À régler` et `Acquittée` (payée intégralement).
- **Calcul automatique** :
  - `À régler` tant que **Montant encaissé TTC < Montant total TTC** (tolérance arrondi 0,01).
  - Passe à `Acquittée` dès que **encaissé ≥ total**.
- **Champs clés** : `TotalTTC`, `EncaisséTTC`, `SoldeTTC` (= Total − Encaissé), `%Réglé`.
- **Partiels** : encaissements partiels possibles (le statut reste `À régler`) avec barre de progression.
- **Avoirs** : impactent le `TotalTTC` de la facture (ou annulent et recréent si nécessaire).
- **Trésorerie “dehors”** : somme des `SoldeTTC` des factures `À régler`, ventilée par **étape** (Commande / Livraison). **Pas de 30/60/90 jours**.

### Conditions de paiement (événementiel uniquement)

- **Modes** :
  - `100% à la commande`,
  - `100% à la livraison`,
  - `En 2 temps` : **Acompte (X%) à la commande** + **Solde à la livraison**.
- **Calcul automatique de X (acompte)** :
  - `T = Total` du document (sans TVA), `C = Σ coûts fournisseurs`, `U = estimation URSSAF`.
  - **Base** `B = C + U`, \*\*p = 100 \* B / T`, **X = ceil(p/10)*10` (arrondi à la dizaine supérieure), **borné à 100%**, **pas de plancher**.
  - **Acompte** = `X% * T`, **Solde** = `T − acompte`.
- **Conversion** d'une facture « 100% commande » vers « 2 temps » : workflow guidé **Avoir total** → **Facture d’acompte** → **Facture de solde**.

### Moyens de paiement paramétrables

- Écran **Moyens de paiement** (CRUD) : nom, **frais fixes**, **commission %**.
- **Frais non répercutés** au client : la facture ne change jamais selon le moyen ; les frais sont enregistrés comme **dépenses** lors de l’encaissement (on stocke `brut / frais / net`).
- Mode **“Autre”** : ouvre une zone de texte libre pour un libellé ponctuel.
- Rapports par moyen (CA, frais, net encaissé).

### Menus déroulants = dictionnaires éditables

- Tout item de liste provient d’un **dictionnaire** gérable dans Réglages (CRUD + import/export CSV) :
  - Taux/Barèmes (URSSAF, versement libératoire),
  - Modes de paiement,
  - Marques/Modèles radios, types/volumes de forfaits, zones (FR/EU),
  - Catégories d’articles/dépenses, unités, dépôts.

### Articles & variantes

- **Produit parent** + **Variantes** (attributs : prise, type casque JET/intégral, etc.).
- SKU/stock/code-barres/prix par **variante**.

### Fiche Client

- Onglets : **Infos**, **Devis**, **Factures**, **Radios & Abonnements**, **Historique**.
- **Actions rapides** : créer devis/facture, copier coordonnées, envoyer email, consulter soldes impayés.
- **Règle d’expiration devis** : tout **devis non transformé en facture** depuis **> 365 jours** est **supprimé automatiquement**.
  - Implémentation :
    - Marquage `Expiré` à J+365 si non converti.
    - **Suppression automatique** avec **corbeille 30 jours** (restauration possible) – paramétrable.

### Import des factures du site web

- Import **manuel** par fichier (PDF/CSV/JSON) ou glisser-déposer ; **pas d’email**.
- Mapping vers clients/articles ; détection de doublons par n° de facture.
- Mise à jour **CA**, **stocks**, **frais passerelle** (si connus) ; file d’erreurs avec résolution assistée.

### Mise en page Devis/Factures & mentions

- **Zones de texte libres** : note **au-dessus** et **en-dessous** du tableau d’articles; modèles de notes réutilisables.
- **Tableau d’articles** : Désignation, Qté, **PU HT**, Remise, **Montant HT**. **Pas de SKU visible**.
- **Totaux** : **Total HT** et **Net à payer** (identiques en micro). **Pas de TVA ni TTC**.
- **Pied** : SIREN/SIRET, adresse, IBAN/BIC/RIB (Réglages).
- **Micro‑entrepreneur** : mention **« TVA non applicable, article 293 B du CGI »** affichée automatiquement et non désactivable tant que le statut est actif.
- **Charte & logo** : entête avec **logo** et coordonnées. Paramétrage **/Assets/Brand/logo.png** (+ couleurs/typos). **Mise en page calquée** sur ton modèle Excel (« Invoice Template ») pour conserver la lisibilité.
- **Zones de texte libres** :
  - **Note en en-tête de lignes** (juste **au-dessus** du tableau d’articles).
  - **Note de pied de lignes** (juste **en-dessous** du tableau d’articles).
  - Sauvegarde de **modèles de notes** (par défauts réutilisables).
- **Tableau d’articles** : colonnes par défaut = **Désignation** (avec variantes), **Qté**, **PU**, **Remise**, **Montant**. **Pas de SKU affiché** (le SKU reste interne).
- **Totaux** : uniquement **Total** (pas de TVA visible nulle part).
- **Pied de document** : affiche **SIREN/SIRET**, **adresse**, et **coordonnées bancaires** (IBAN/BIC/RIB) — le tout **paramétrable**.
- **Mentions légales** :
  - Si **Statut = Micro‑entrepreneur (franchise de TVA)** est activé dans **Réglages > Profil fiscal**, la mention **« TVA non applicable, art. 293 B du CGI » est affichée automatiquement** sur **tous les devis et factures** et **n’est pas désactivable** tant que ce statut est actif.
  - Si plus tard tu changes de statut (assujetti TVA), on **retire la mention** et on **active** les modules TVA (taux, bases, lignes TVA) ; l’option devient alors gérable.

### Import des factures du site web

- Import **manuel** par fichier (PDF/CSV/JSON) ou glisser-déposer ; **pas d’email**.
- Mapping vers clients/articles ; détection de doublons par n° de facture.
- Mise à jour **CA**, **stocks**, **frais passerelle** (si connus) ; file d’erreurs avec résolution assistée.

### Emails sortants

- **SMTP configurable** (Hostinger, etc.), modèles d’email, PDF en PJ, Outbox et journal.

### Annexes & documents complémentaires (devis/factures)

- **Sélection d’annexes existantes (PDF)** au moment de l’émission : **multi‑sélection** dans une liste, avec **ordre** ajustable (haut/bas). **Pas de sommaire**.
- **Catalogue d’annexes** dans **Réglages > Annexes** : tableau CRUD pour **ajouter/retirer** des annexes prédéfinies.
  - Champs : *Nom visible*, *Chemin relatif* (ex. `/Assets/Annexes/CGV.pdf`), *Type* (CGV, Garantie, Clause…), *Actif* (oui/non).
  - Les fichiers sont **déjà des PDF** (tu fournis tes CGV, garanties, clauses…). L’app **les concatène** tels quels en fin de document.
- **Annexe dynamique “Présentation des objets”** :
  - Case à cocher → ouvre un **sélecteur d’articles du devis** (par défaut tous cochés).
  - Pour chaque ligne : **inclure / exclure**, **image** (si dispo), **titre** (éditable), **description courte** (optionnelle, param par article), **réf. interne** optionnelle (SKU non affiché par défaut).
  - Mise en page **simple** : grille 3 colonnes (image au‑dessus, titre en dessous, description courte). **Pas de prix** (on reste en annexe de présentation).
  - S’il manque une image, on affiche **juste le titre** (pas de placeholder visuel).
- **Pipeline PDF** : document principal (devis/facture) → **annexe dynamique** (si cochée) → **PDF externes** (dans l’ordre choisi). Compression images intégrée (\~120–150 DPI).

### Gestion des médias produits (images)

- Chaque **article/variante** peut référencer **1 image principale** + **galerie** (chemins relatifs), ex. `/Assets/Images/Articles/{SKU}/main.jpg`.
- **Compression intégrée** à l’export PDF (cible \~120–150 DPI) pour limiter la taille, sans dépendre d’internet.
- **Présentation simple** pour la planche visuelle : grille 3 colonnes, **image** + **nom article/variante** + description courte. **Pas de SKU ni de prix** visibles par défaut.
- Option **“vignette dans le tableau”** : petite image à gauche de la désignation — activable/désactivable dans Réglages.

### Mise en page Devis/Factures & mentions (rappel)

- **Zones de texte libres** : note **au-dessus** et **en-dessous** du tableau d’articles; modèles de notes réutilisables.
- **Tableau d’articles** : Désignation, Qté, PU, Remise, Montant. **Pas de SKU visible**.
- **Totaux** : uniquement **Total** (aucune TVA affichée).
- **Pied** : SIREN/SIRET, adresse, IBAN/BIC/RIB (Réglages).
- **Micro‑entrepreneur** : mention **« TVA non applicable, art. 293 B du CGI »** affichée automatiquement et non désactivable tant que le statut est actif.

### Gestion des médias produits (images)

- Chaque **article/variante** peut référencer **1 image principale** + **galerie** (chemins relatifs), ex. `/Assets/Images/Articles/{SKU}/main.jpg`.

- **Compression intégrée** à l’export PDF (cible \~120–150 DPI) pour limiter la taille, sans dépendre d’internet.

- **Présentation simple** pour la planche visuelle : grille 3 colonnes, **image** + **nom article/variante** + description courte. **Pas de SKU ni de prix** visibles par défaut.

- Option **“vignette dans le tableau”** : petite image à gauche de la désignation — activable/désactivable dans Réglages.

- Chaque **article/variante** peut référencer **1 image principale** + **galerie** (chemins relatifs), ex. `/Assets/Images/Articles/{SKU}/main.jpg`.

- **Compression intégrée** à l’export PDF (cible \~120–150 DPI) pour limiter la taille, sans dépendre d’internet.

- **Présentation simple** pour la planche visuelle : grille 3 colonnes, **image** + **nom article/variante** + option **réf. interne** (mais **pas de SKU** visible sur PDF client par défaut).

- Option **“vignette dans le tableau”** : petite image à gauche de la désignation — activable/désactivable dans Réglages.

### Mise en page Devis/Factures & mentions (rappel)

- **Zones de texte libres** : note **au-dessus** et **en-dessous** du tableau d’articles; modèles de notes réutilisables.
- **Tableau d’articles** : Désignation, Qté, PU, Remise, Montant. **Pas de SKU visible**.
- **Totaux** : uniquement **Total** (aucune TVA affichée).
- **Pied** : SIREN/SIRET, adresse, IBAN/BIC/RIB (Réglages).
- **Micro‑entrepreneur** : mention **« TVA non applicable, art. 293 B du CGI »** affichée automatiquement et non désactivable tant que le statut est actif.

---

## Synthèse lecture des fichiers (13/09/2025)

**Devis/Factures (modele\_linked.xlsm)** : feuilles Clients, Articles (Code, Libellé, Type, PrixAchatHT, PrixVenteHT, TVA, StockActuel, TauxCotisation…), Ventes (TypeDoc), Ventes\_Lignes (PU\_HT, TVA, Total\_HT, TauxCotisation), Mouvements\_Stock, Stats, Cotisations, Parametres, listes, Invoice Template. Modules VBA et UserForms exportés détectés.

**Parc radios/SIM (ParcPOC\_SIM.xlsm)** : feuilles Clients, Radios (RadioBarcode, Modèle, ClientNomAff, GroupeCall…), SIMs (SimBarcode, Pays, ForfaitCode, ClientNomAff, DateCreate), Parc (AssocID, RadioBarcode, SimBarcode, IDOTA, OTAPW, ValidTime, MonthlyActiveCost / SuspCost…), Alertes, Listes, Param. Un lien inter‑classeur pointe vers le classeur Devis/Factures via un chemin absolu (non portable).

### Écarts / conflits à valider

1. **TVA/HT** présents dans les colonnes alors que tu es en micro : on cache toute TVA à l’écran et sur PDF et on force TVA=0 à l’import (ou on renomme les champs en PrixAchat / PrixVente). **OK pour toi ?**
2. **Cotisation** par ligne via TauxCotisation (Articles et Ventes\_Lignes) : on garde un taux par nature d’article (Service/Marchandise) avec override possible par ligne. **OK ?**
3. **Chemins absolus / dépendances Excel↔Excel** : on supprime et on remplace par base SQLite + chemins relatifs sur la clé. **On tranche comme ça ?**
4. **Mise en page Excel** : on remplace par génération PDF côté appli avec annexes cochables et planche produits. **On valide ?**
5. **Devis > 365 j non transformés** : suppression auto (avec corbeille 30 j). **On confirme ?**

### Mappings proposés (résumé)

- Clients ← feuille Clients.
- Articles ← feuille Articles (Code→SKU, Type→Nature, prix achat/vente, stock, seuil, taux cotisation par défaut).
- Devis/Factures ← Ventes + Ventes\_Lignes (TVA ignorée), modes de paiement ← listes.
- Stocks ← Mouvements\_Stock + stock actuel.
- Cotisations ← Cotisations (dictionnaire).
- Parc radios/SIM ← Radios, SIMs, Listes, Parc (attributions et coûts).

### 4G / POC / SIM / OTA — compléments détectés dans tes fichiers

- **Identifiants & liaisons**
  - `RadioBarcode` (radios), \`\` (SIM) — **ICCID = code‑barres SIM** (identifiant **unique**). **SIM M2M** : **pas de MSISDN**, **IMSI non stocké**.
  - Table **Parc** = association Radio↔SIM : on **historicise** avec `DateDebut/DateFin` + `Statut` (actif/suspendu/perdu).
- **Groupes & comptes POC**
  - `GroupeCall` / `DefaultGroup` (talkgroups) par radio + **historique** (conserve les `Old_*` vus dans `POC_LastImport`).
  - `Username/Account/Password` POC : stockage **chiffré**, champ **masqué avec icône “œil”** (afficher/masquer) + **copie** (journalisée).
- **OTA / provisioning**
  - `IDOTA`, `OTAPW`, `ValidTime` → stockés par radio, chiffrés. **ValidTime = interne uniquement** (exigence confirmée).
- **Forfaits & coûts** (évolutifs)
  - Réglages > **Forfaits** : `ForfaitCode`, `ZoneSIM` (FR/EU/CH/Monde…), `Operateur`, `VolumeData`, `ActiveCost`, `SuspCost`, `CycleFacturation`.
  - **Versions** (périodes d’effet) pour gérer les changements de tarifs/volumétrie.
- **Modèle “actif/suspendu” sur 12 mois** (activation inconnue initialement)
  - Paramètres du **contrat** : `DateDebutContrat`, `DureeMois` (ex. 12), `MoisActifsPlanifies` (ex. 6 ou 9), `MoisSuspendusPlanifies = Duree - MoisActifs`.
  - **Phase suspendue** : à partir de `DateDebutContrat` et **jusqu’à l’activation**. On consomme les **mois suspendus** par **mois calendaire**.
  - **Activation** (action manuelle sur une **liste de SIM**) : envoi du mail prestataire “Demande d’activation”, on fixe `DateActivation = aujourd’hui`.
  - **Règle de mois actif n°1** : le **mois civil de l’activation compte comme 1er mois actif**, **même si** l’activation intervient le même mois que le **début de contrat** (dans ce cas, **0 mois suspendus** consommés).
  - **Phase active** : on compte `MoisActifsPlanifies` à partir de ce mois civil. **DateSuspensionPrévue** = **dernier jour** du **dernier mois actif** (EOM).
- **Alertes & notifications**
  - **Client** : \*\*uniquement sur \*\*` (fin de contrat). **Aucune notif client sur **`.
  - **Interne (toi)** :
    - Alerte **J‑7** avant **DateSuspensionPrévue (EOM)**.
    - **Mail prestataire – Demande de suspension** : programmé **J‑1** avant la fin du mois de suspension (envoi 09:00, Europe/Paris).
  - **Activation / Suspension manuelles** : sélection de SIM → boutons **Activer** / **Demander suspension** → mails générés via **modèles**.
- **Paramétrage email & modèles**
  - Réglages > **Courriel** (SMTP, From, Reply-To, BCC, test de connexion).
  - Réglages > **Modèles d’emails** : Client (ContractEnd), Prestataire (Activation / Suspension) — **gabarits dynamiques** et **envoi groupé par client**.

### Groupes d’appel (talkgroups) — conception

- **Dictionnaire “Groupes”** (global) : `Id`, `Nom`, `CodePlateforme?` (option), `Actif`, `DateMAJ`.
- **Gestion** : écran **Groupes** (CRUD) + **fusion** (merge) et **renommage**; import/export CSV.
- **Affectation** :
  - Radio ↔ Groupes : **plusieurs groupes par radio**; un **DefaultGroup** par radio.
  - **Listes rapides** : assigner/retirer un groupe à **N radios** (sélection).
- **Import DeviceList POC** : à l’import, on **déduit** la liste des groupes présents →
  - nouveaux noms **proposés à l’ajout** au dictionnaire,
  - **résolution des doublons** (ex. “Team A” vs “TEAM A”) avec suggestion de fusion,
  - **journal** des changements (ancien → nouveau nom).
- **Alias client (option)** : possibilité d’un **alias par client** si un même groupe doit apparaître sous un nom différent chez un client (désactivé par défaut).

### Import Device List (POC) — .XLS / .CSV

- Formats supportés : **.xls** (Excel 97–2003) via **ExcelDataReader** (C#) et **.csv**.
- **Mapping générique** (détecté/paramétrable) : `DeviceId/Username/Account`, `Alias`, `GroupeCall`, `DefaultGroup`, `ICCID`, `ValidTime`, `Client?` (si fourni), `Etat`.
- **Rapprochement** :
  - Compare avec dernier **snapshot** importé → calcule **différences** (groupe changé, compte changé, ICCID changé, etc.).
  - Impacte **Parc** (radio/SIM/client) et **Groupes** (ajout/merge) selon règle.
  - Drapeaux **à valider** avant application (tu choisis ce qui est poussé en base).
- **Sécurité** : aucun mot de passe POC depuis DeviceList; les credentials restent gérés côté app (masqués/visibles à la demande).

### CHIRP – Intégration des profils radio (UV‑5R, RT86)

**Constat à partir de tes fichiers** : `.img` = images binaires CHIRP par **modèle** et **contexte client** (ex. `Baofeng_UV-5R_ULMER`, `Retevis_RT86_Opale_Racing`, profils nominatifs `1005_...`). Le contenu (canaux, fréquences, tones, Large/Narrow, puissance, noms) n’est **pas en clair** dans `.img`.

**Stratégie d’app** :

- **Données structurées** ↔ **CSV CHIRP** : on **importe/édite** un **gabarit commun** de canaux dans l’app, puis on **exporte** au format **CSV CHIRP** pour flasher avec CHIRP (l’image `.img` restera ton backup/trace, jointe au profil).
- **.img** : stocké en **pièce jointe** du **Profil Radio** pour tracabilité/versionning, sans parsing direct.

**Schéma “Canal commun” (v1)** :

- `ChannelNo`, `Name` (alias), `RX_Freq`, `TX_Freq` (ou `Offset` + `Duplex +/−/split`), `Step`, `Power (L/M/H)`, `W/N (12.5/25)`,
- `ToneMode (None/Tone/DCS/DTCS)` , `CTCSS_Tone`, `CTCSS_SQL`, `DCS_Code`, `DCS_Polarity`,
- `ScanAdd`, `BusyLock/BCL`, `PTT_ID/DTMF?` (si utilisé), `Comment`.

**Contraintes par modèle** (exemples) :

- **UV‑5R** : `Name` 6–7 chars, bande 136–174 / 400–520, options Narrow/Large, High/Low.
- **RT86** : champs proches mais jeux de valeurs parfois différents ; on gère via **mappages modèle**.

**Entités** :

- **RadioModel** (UV‑5R, RT86…) → capacités (bandes, step, tailles Nom, etc.).
- **RadioProfile** (ex. *UV5R\_ULMER\_2025‑09‑14\_v1*), `Client`, `Site/Événement`, `Notes`, **fichier .img attaché**.
- **ProfileChannels** (table des canaux normalisée) + **ProfileExports** (CSV générés, versionnés).

**Règles UHF** :

- **Détection de conflits** entre profils par **fréquence/largeur** et **zone** (site/événement).
- **Tolérance** : même fréquence **autorisée pour un même client** si **sites auto distants** (enregistrer `Site` et `Zone`).

**Annexes PDF** :

- **Planche “Liste des canaux”** (alias + RX/TX + tone + puissance + largeur) pour le client/atelier ; QR du **ProfileCode**.

**Workflow** :

1. Import **CSV CHIRP** existant → **profil** + canaux normalisés.
2. Édition **dans l’app** (gabarit commun, duplication rapide par radio/personne).
3. Export **CSV CHIRP** par **modèle** (mapping), puis flash via CHIRP.
4. Joindre l’**.img final** au **Profil** (trace).

**Recherche** : la **barre universelle** retrouvera un **Profil** par `Name`, `Client`, `RadioBarcode` ; ouverture rapide de la **planche canaux**.

### Plan de fréquences — import & règles (à partir de « Frequances.xlsx »)

- **Feuilles détectées** : import des **2 feuilles** (aperçus déposés). Colonnes repérées : `Frequence/MHz` (ou `RX/TX`), `Client`, `Site/Zone` (selon feuille), `Canal/Nom`, `Largeur (W/N)`, `Tone (CTCSS/DCS)`, `Pas/Step`.
- **Nettoyage à l’import** : virgules → points pour les MHz, trim, détection des doublons, normalisation des noms.
- **Conflits** : première passe → **pas de conflit multi‑client évident** sur **une même fréquence** (dans une feuille), à confirmer avec les 2 vues croisées.
- **Modèle cible** :
  - `Frequencies` (MHz, largeur, pas, bande),
  - `Sites` (Nom, Zone, Coordonnées optionnelles),
  - `Clients`,
  - `Assignments` (FreqId, ClientId, SiteId, DateDebut, DateFin, Commentaire),
  - `Profiles` (pour la cartouche CHIRP), `ProfileChannels` (liés aux fréquences).
- **Règles d’attribution** :
  - **Interdiction** par défaut : même fréquence **à deux clients différents** **sur le même site/zone**.
  - **Tolérance** : **autorisé** pour **le même client** si **sites auto distants** (on stocke le Site et la Zone pour vérifier).
  - Alerte si **largeur** différente pour la **même fréquence** dans un même site.
- **Workflow** :
  1. Import du fichier **Frequances.xlsx** → stockage en **tables normalisées**.
  2. Écran **Pool** (frequences disponibles) + **Affectations** (par client et par site).
  3. Génération **profil CHIRP** (sélection de fréquences), puis export **CSV** par modèle.
  4. Annexes PDF **Plan des canaux**.



### Bornage fréquences UHF (validé)

- **Bandes autorisées** :
  - **430,000–440,000 MHz** (UHF)
  - **440,000–470,000 MHz** (UHF)
- **Zone d’exclusion** : **PMR446** → **446,000–446,200 MHz** (canaux PMR446) **non attribuables**.
- **Moteur de proposition** : ne propose que des canaux **dans les bandes autorisées**, **hors zones d’exclusion**, en respectant le **pas** configuré (**12,5 kHz** par défaut, **6,25 kHz** optionnel).
- **Contrôles** : alerte **collision** si fréquence déjà utilisée (ou trop proche ±½ pas) et **refus automatique** si saisie dans une zone exclue.

