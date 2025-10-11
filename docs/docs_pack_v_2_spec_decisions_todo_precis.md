Below are **ready-to-commit** files. Copy each block to the matching file path in your repo.

---

# File: docs/SPEC.md

## 1) Vision & périmètre

Remplacer les Excel/VBA par une application **portable** sur Windows (clé USB), sans installation, couvrant :
- **Devis & Factures** (micro-entrepreneur, *zéro TVA affichée*)
- **Clients** & **Articles** (+ variantes, images, codes-barres)
- **Stocks** (seuil mini, mouvements)
- **Modes de paiement** (frais fixes & % non répercutés au client)
- **Caisse légère** (journal des encaissements)
- **Flotte radios 4G / POC / SIM / OTA** (imports, affectations, alertes)
- **UHF** (plans de fréquences, profils CHIRP, exports CSV)

> EXE **self-contained** (net8.0-windows x64), données sous-dossier de l’EXE.

---

## 2) Environnement & conventions

- **Technos** : .NET 8, WPF, SQLite.
- **Libs** : Microsoft.Data.Sqlite, CsvHelper, PdfSharp-MigraDoc-GDI, System.IO.Ports.
- **Culture** : `fr-FR` (format monétaire français), séparateur décimal `,` à l’écran, PDF formaté `0,00 €`.
- **Fuseau** : Europe/Paris.
- **Thème sombre** : palette `Themes/Dark.xaml` (déjà en place). Tokens obligatoires : `Bg, Panel, Field, Border, HeaderBg, Fore, Subtle, Accent, Select`.
- **Navigation** : barre latérale boutons (Dashboard, Clients, Articles, Devis, Factures, Réglages). Dashboard au démarrage.
- **Chemins** : via `Paths.cs` (déjà présent).

```
DataDir   = ./Data
AssetsDir = ./Assets
LogsDir   = ./Logs
ConfigDir = ./Config
DbPath    = ./Data/app.db
Settings  = ./Config/settings.json
```

---

## 3) Modèles de données (côté app & DB)

### 3.1 Réglages (JSON) — *persistant*

Fichier `Config/settings.json` (créé si absent). Schéma minimal garanti :

```json
{
  "BusinessName": "",
  "Siret": "",
  "Iban": "",
  "Bic": "",
  "IsMicro": true,
  "PaymentMethods": [
    { "Name": "Espèces", "FixedFee": 0, "PercentFee": 0 },
    { "Name": "Virement", "FixedFee": 0, "PercentFee": 0 },
    { "Name": "CB",       "FixedFee": 0, "PercentFee": 1.5 },
    { "Name": "Chèque",   "FixedFee": 0, "PercentFee": 0 }
  ]
}
```

Règles :
- `IsMicro = true` → **affiche obligatoirement** la mention « TVA non applicable, art. 293 B du CGI » sur **tous** devis & factures.
- `PaymentMethods` : frais **non répercutés** au client (comptabilisés en dépense à l’encaissement).

### 3.2 Entités métier (DB SQLite — v1)

> DB à mettre en place dans `Data/app.db` (migration initiale). Clés `INTEGER PRIMARY KEY AUTOINCREMENT`.

**Clients**
- `Id`, `Nom`, `AdresseL1`, `AdresseL2`, `CP`, `Ville`, `Email`, `Tel`, `Notes`.

**Articles**
- `Id`, `SKU`, `Libelle`, `DescriptionCourte`, `PrixAchat`, `PrixVente`, `Unite`, `ImageMain`, `Actif`.

**Variantes**
- `Id`, `ArticleId`, `AttributsJson` (ex: {"Taille":"M","Couleur":"Noir"}), `SKU`, `PrixVente`, `Image`.

**Stock**
- `Id`, `ArticleId` (*ou* `VarianteId`), `Depot`, `Qte`, `SeuilMini`.

**Devis**
- `Id`, `Numero` (`DEV-AAAA-####`), `ClientId`, `Date`, `Etat` (`Brouillon`, `Envoye`, `Transforme`, `Expire`), `Total`.

**DevisLignes**
- `Id`, `DevisId`, `Designation`, `Qty`, `PU`, `Remise` (valeur €), `Montant`.

**Factures**
- `Id`, `Numero` (`FAC-AAAA-####`), `ClientId`, `Date`, `Total`, `EncaisseTTC`, `Statut` (`A_REGler`, `ACQUITTEE`).

**FacturesLignes**
- `Id`, `FactureId`, `Designation`, `Qty`, `PU`, `Remise`, `Montant`.

**Encaissements**
- `Id`, `FactureId`, `Date`, `Mode`, `Brut`, `Frais`, `Net`.

(UHF/POC/SIM → tables spécifiques v2, voir §8)

---

## 4) Règles métier clés

### 4.1 Numérotation
- Par type et par année : `DEV-2025-0001`, `FAC-2025-0001`.
- Paramétrable dans Réglages (séries), mais défaut si absent.

### 4.2 Devis
- Statuts : `Brouillon` → `Envoye` → `Transforme` (vers facture).
- Suppression auto des **devis > 365 j non transformés** (corbeille 30 j).

### 4.3 Factures
- Deux statuts : `A_REGler` / `ACQUITTEE`.
- Calcul auto : `ACQUITTEE` dès que `EncaisseTTC ≥ Total` (tolérance 0,01).
- Conversion « 2 temps » (acompte + solde) → plus tard (workflow dédié).

### 4.4 Moyens de paiement
- Frais fixes et % **non répercutés** (journalisés dans Encaissements : `Brut/Frais/Net`).

### 4.5 Micro-entrepreneur
- **Pas de TVA visible** nulle part.
- Mention **obligatoire** sur PDF lorsque `IsMicro = true`.

### 4.6 Stocks
- Décrément **à la facturation** (pas au devis).
- Alerte UI si `Qte < SeuilMini`.
- **Packs/Kits** (v2) : décrément des composants.

---

## 5) UI & UX

- **Dashboard** (KPI basiques: total mois, à régler, ruptures imminentes).
- **Clients** (liste + fiche avec onglets : Infos, Devis, Factures, Parc radios, Historique).
- **Articles** (liste + variantes + images ; import CSV).
- **Devis** (CRUD, transformation en facture, PDF, annexes futures).
- **Factures** (CRUD, PDF, statut, encaissements, export compta plus tard).
- **Réglages** :
  - Profil fiscal (Micro), Identité (SIRET/IBAN/BIC/BusinessName), Moyens de paiement, Email SMTP (plus tard), Annexes (plus tard).
- **Dark theme** : contrastes conformes (hover/active lisibles). DataGrid sans grilles dures, sélection `Select`.

---

## 6) PDF (MigraDoc)

- Fonte : Segoe UI (ou fallback sys si absente).
- En-tête : logo si `Assets/Brand/logo.png`, nom commercial à droite.
- Cartouche : `N°` + `Date` + bloc destinataire.
- Tableau : `Désignation`, `Qté`, `PU`, `Montant` (alignés à droite sauf désignation).
- Totaux : **Total** (HT=TT en micro).
- Mention 293B **automatique** si `IsMicro`.
- Pied : `SIRET`, `IBAN`, `BIC` si fournis.
- Annexes : pipeline prévu (annexe dynamique planche produits + PDF externes, v2).

---

## 7) Imports CSV (v1)
- Mappage colonnes ↔ champs, prévisualisation, rapport d’erreurs.
- Modes : Créer / Mettre à jour / Ignorer.
- Normalisation : nombres FR, trim, doublons par clé (SKU, Email, etc.).

---

## 8) Radios 4G / POC / SIM / OTA (v2)
- Entités : `Radios`, `SIMs` (ICCID unique), `Parc` (assoc Radio↔SIM avec historique).
- Groupes d’appel (dictionnaire + assignations multiples).
- Imports DeviceList (CSV/XLSX) avec diff et validations.
- Crédentials POC **chiffrés**, masqués par défaut (icône œil) + journal d’accès.
- Alertes : activation/suspension, J‑15/J‑7.

---

## 9) UHF (v2)
- Fréquences avec bandes & pas, zones d’exclusion (PMR446), propositions de canaux sûrs.
- Profils CHIRP : gabarit normalisé → export CSV par modèle (UV‑5R, RT86…).
- Détection de collisions par site/zone (tolérance client même fréquence si sites distants du même client).

---

## 10) Journalisation & sauvegardes (v2)
- Serilog fichiers roulants `./Logs/*.log` (30 j).
- Sauvegardes `./Backups/`, ZIP chiffré (plus tard), rotation hebdo.

---

## 11) Sécurité & rôles (v2)
- Rôles : Admin / Opérateur / Lecture seule. Accès secrets POC réservé Admin.
- Audit : affichage PW, envois prestataire, imports, suppressions.

---

## 12) Performance & tests
- Pagination listes, index DB sur FK/Numero.
- Tests unitaires sur services (numérotation, calcul statut facture, parse CSV).

---

## 13) Publication & portabilité

```
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false -o C:\VorTechBuild
```
Copier le dossier sur la clé. Vérifier écriture dans `Data/`, `Config/`, `Logs/`.

---

# File: docs/DECISIONS.md

## D1. Architecture
- **Monoproj** `VorTech.App` (actuel) conservé pour v1. Découpage par dossiers `Models/Services/Views/data/Assets`.
- Migration **multi‑projets** possible v3.

## D2. Persistance
- **SQLite** local `Data/app.db` pour données métier.
- **Réglages** en **JSON** (`Config/settings.json`) via `ConfigService`.

## D3. ORM
- **ADO + SQL direct** / micro‑DAL (simple) prioritaire. Dapper envisageable v2 si besoin.

## D4. PDF
- **MigraDoc** (PdfSharp-MigraDoc-GDI) adopté. Plus de `PdfFontEmbedding` (API moderne). Unicode OK d’office.

## D5. Thème & UI
- Thème sombre unique. DataGrid sans grilles, sélection nette, hover boutons lisible.

## D6. Numérotation
- Séries par type/année, format `TYPE-YYYY-####`. Stockage en table ou settings (v1 en settings suffisant).

## D7. TVA
- Mode **micro** = pas de TVA nulle part, mention 293B obligatoire.

## D8. Frais moyens de paiement
- Jamais répercutés au client. Comptabilisés côté **Encaissements**.

## D9. Imports
- CSV only en v1 (XLSX via ExcelDataReader plus tard). Mappage souple + prévisu.

## D10. Sécurité
- Pas d’internet requis. Aucun secret externe stocké en clair (POC chiffré v2).

## D11. Sauvegardes
- ZIP dans `Backups/` (chiffrage + rotation plus tard). Export « pack support ».

## D12. Simulation
- Mode test : base temporaire, pas d’envoi réel (email/COM), badge « SIMULATION ».

---

# File: docs/TODO.md

Légende: ✅ fait · 🚧 en cours · ⏳ à faire · 🧪 test · 🐞 bug

## A. Socle & réglages
- ✅ `Paths.cs` (racine portable)
- ✅ `ConfigService` (CRUD JSON, défauts PaymentMethods)
- ✅ `SettingsView` (bindings bi‑directionnels, sauvegarde)
- ⏳ `Docs` (ce pack) → **commiter**
- ⏳ Logger Serilog minimal

## B. PDF
- ✅ `InvoicePdf.RenderSimpleInvoice` (micro, fr-FR, 293B auto)
- ⏳ Ajout **notes libre haut/bas**
- ⏳ Annexes PDF pipeline (dynamique planche + PDFs)
- 🧪 Visuels : logo / typos / marges

## C. Données & DB (v1)
- ⏳ Script création DB (SQLite) + DAL simple (Clients/Articles/Stock/Devis/Factures)
- ⏳ Services : `ClientService`, `ArticleService`, `StockService`, `DevisService`, `FactureService`, `EncaissementService`
- ⏳ Numérotation séries en settings + service

## D. UI CRUD
- ⏳ `ClientsView` (liste + fiche)
- ⏳ `ArticlesView` (variantes + images + import CSV)
- ⏳ `DevisView` (lignes, remise globale € optionnelle masquée si 0)
- ⏳ `InvoicesView` (encaissements, statut auto)
- ⏳ `DashboardView` (KPI)

## E. Imports
- ⏳ CSV mapping générique (Clients/Articles)

## F. UHF / 4G / POC (v2)
- ⏳ Modèle DB + vues Parc radios/SIM, Groupes, Imports DeviceList
- ⏳ Exports CHIRP CSV + planche canaux PDF

## G. Qualité & livraison
- ⏳ Tests unitaires Services (numérotation, statut facture, CSV parse)
- ⏳ Publication Release portable + README d’usage

---

# (Optionnel) File: docs/DB_SCHEMA.sql

```sql
-- Clients
CREATE TABLE IF NOT EXISTS Clients (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom TEXT NOT NULL,
  AdresseL1 TEXT, AdresseL2 TEXT, CP TEXT, Ville TEXT,
  Email TEXT, Tel TEXT, Notes TEXT
);

-- Articles
CREATE TABLE IF NOT EXISTS Articles (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  SKU TEXT UNIQUE,
  Libelle TEXT NOT NULL,
  DescriptionCourte TEXT,
  PrixAchat REAL DEFAULT 0,
  PrixVente REAL DEFAULT 0,
  Unite TEXT,
  ImageMain TEXT,
  Actif INTEGER DEFAULT 1
);

-- Variantes
CREATE TABLE IF NOT EXISTS Variantes (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  ArticleId INTEGER NOT NULL,
  AttributsJson TEXT,
  SKU TEXT UNIQUE,
  PrixVente REAL,
  Image TEXT,
  FOREIGN KEY(ArticleId) REFERENCES Articles(Id)
);

-- Stock
CREATE TABLE IF NOT EXISTS Stock (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  ArticleId INTEGER,
  VarianteId INTEGER,
  Depot TEXT DEFAULT 'PRINCIPAL',
  Qte REAL DEFAULT 0,
  SeuilMini REAL DEFAULT 0,
  FOREIGN KEY(ArticleId) REFERENCES Articles(Id),
  FOREIGN KEY(VarianteId) REFERENCES Variantes(Id)
);

-- Devis
CREATE TABLE IF NOT EXISTS Devis (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Numero TEXT UNIQUE,
  ClientId INTEGER NOT NULL,
  Date TEXT NOT NULL,
  Etat TEXT DEFAULT 'Brouillon',
  Total REAL DEFAULT 0,
  FOREIGN KEY(ClientId) REFERENCES Clients(Id)
);

CREATE TABLE IF NOT EXISTS DevisLignes (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  DevisId INTEGER NOT NULL,
  Designation TEXT NOT NULL,
  Qty REAL DEFAULT 1,
  PU REAL DEFAULT 0,
  Remise REAL DEFAULT 0,
  Montant REAL DEFAULT 0,
  FOREIGN KEY(DevisId) REFERENCES Devis(Id)
);

-- Factures
CREATE TABLE IF NOT EXISTS Factures (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Numero TEXT UNIQUE,
  ClientId INTEGER NOT NULL,
  Date TEXT NOT NULL,
  Total REAL DEFAULT 0,
  EncaisseTTC REAL DEFAULT 0,
  Statut TEXT DEFAULT 'A_REGler',
  FOREIGN KEY(ClientId) REFERENCES Clients(Id)
);

CREATE TABLE IF NOT EXISTS FacturesLignes (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  FactureId INTEGER NOT NULL,
  Designation TEXT NOT NULL,
  Qty REAL DEFAULT 1,
  PU REAL DEFAULT 0,
  Remise REAL DEFAULT 0,
  Montant REAL DEFAULT 0,
  FOREIGN KEY(FactureId) REFERENCES Factures(Id)
);

-- Encaissements
CREATE TABLE IF NOT EXISTS Encaissements (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  FactureId INTEGER NOT NULL,
  Date TEXT NOT NULL,
  Mode TEXT NOT NULL,
  Brut REAL NOT NULL,
  Frais REAL DEFAULT 0,
  Net REAL NOT NULL,
  FOREIGN KEY(FactureId) REFERENCES Factures(Id)
);
```

