# VorTech.App — Spécification fonctionnelle (SOURCE DE VÉRITÉ)

## 1) Objectif
Remplacer les fichiers Excel/VBA par une application **portable** (clé USB) simple à maintenir :
- Devis / Factures / Clients / Articles / Stock / Caisse légère
- Parc radios **UHF** + profils CHIRP (export CSV), règles d’attribution fréquences
- Parc **4G/POC/OTA/SIM/Abonnements** (activation/suspension, coûts récurrents)

## 2) Périmètre v1 (priorités)
- CRUD Clients / Articles / Stock
- Devis → Factures (numérotation par série/année)
- PDF propres (MigraDoc), **mode micro (pas de TVA affichée)**
- Réglages persistés (JSON) + moyens de paiement (frais non répercutés)
- Import CSV Clients/Articles
- Base SQLite portable (fichier `Data/app.db`) – branchement progressif

## 3) Stack
- **.NET 8 + WPF**  
- **SQLite** (`Microsoft.Data.Sqlite`)  
- PDF : **PdfSharp/MigraDoc**  
- CSV : **CsvHelper**  
- Ports série : **System.IO.Ports**  
- MVVM (progressif) : CommunityToolkit.Mvvm (optionnel v1)

## 4) Structure projet (aujourd’hui)
- **Mono-projet** : `VorTech.App/` (WPF)
- Dossiers :
  - `Assets/` (logo, annexes…), `Config/` (settings.json), `Data/` (app.db), `Logs/`
  - `Models/`, `Services/`, `Views/`, `Themes/`
- Chemins centralisés dans `Paths.cs`

> Évolution possible plus tard vers solution multi-projets (Core/Data/Reports…).

## 5) Chemins & portabilité
- Racine app : `AppContext.BaseDirectory`
- `Data/`, `Assets/`, `Config/`, `Logs/` → créés si absents
- Exécutable self-contained (pas d’installation), prévu pour clé USB

## 6) Réglages (JSON)
- Fichier : `Config/settings.json` (modèle `Models/AppConfig.cs`)
- Service : `Services/ConfigService.cs` (Load/Save)
- Champs clés : `BusinessName`, `Siret`, `Iban`, `Bic`, `IsMicro`, `PaymentMethods[]` (Nom, FixedFee, PercentFee)

## 7) PDF (MigraDoc)
- Entête : logo `/Assets/Brand/logo.png` si présent + raison sociale
- Tableau : Désignation, Qté, PU, Montant
- **Total uniquement** (mode micro : pas de TVA/TTC)
- **Mention 293 B** auto si `IsMicro = true`
- Pied : SIRET + IBAN/BIC (si renseignés)
- Annexes (plus tard) : concat PDF existants + “planche produits”

## 8) UI / Thème
- Thème sombre (`Themes/Dark.xaml`), couleurs lisibles (contraste OK)
- Navigation latérale simple (Dashboard, Clients, Articles, Devis, Factures, Réglages)
- Écrans simples et lisibles, textes 14pt par défaut

## 9) Règles métier principales (actées)
- **Micro-entreprise** : pas de TVA nulle part (affichage ni calcul)
- **Mention 293 B** forcée tant que statut micro actif
- Devis **> 365 jours** non transformés : **suppression auto** (avec **corbeille 30j**)
- Moyens de paiement : **frais non répercutés** (impact sur dépenses, pas sur facture)
- Numérotation **multi-séries par année** (ex.: `DEV-2025-0001`, `FAC-2025-0001`)
- Articles & variantes (SKU par variante), seuil mini de stock + alerte
- Import CSV mappé (prévisu, validation)

## 10) UHF (v1)
- Dictionnaire de fréquences, sites/zones, attributions Clients/Sites
- **Tolérance** : même fréquence **autorisée pour un même client** si sites **auto distants**
- **Exclusion PMR446** (446.000–446.200 MHz)
- Export **CSV CHIRP** par modèle radio (UV-5R, RT86…) à partir d’un gabarit commun

## 11) 4G/POC/SIM (v1)
- SIM (ICCID), radios POC, comptes/Groupes (talkgroups)
- Activation/Suspension avec **modèles d’emails** prestataire
- Rappels internes J-15 (contrats/échéances) – pas de mails clients automatiques
- Coûts récurrents → MRR-like (vision CA/frais)

## 12) Publication portable
- csproj : `<SelfContained>true</SelfContained>`, `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`
- Commande : `dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false -o C:\VorTechBuild`

## 13) Sécurité & logs
- **Serilog** fichiers tournants dans `Logs/`
- SMTP configurable (plus tard) – pas d’envoi en mode Simulation
- Secrets POC masqués (icône “œil”), copie journalisée (plus tard)

## 14) Roadmap (résumé)
- M1 : Réglages JSON + PDF OK + CRUD basiques + import CSV
- M2 : SQLite branché (Clients/Articles/Devis/Factures) + numérotation séries
- M3 : 4G/POC/SIM (activation/suspension) + MRR-like
- M4 : UHF v1 (attributions + export CHIRP)
- M5 : sauvegardes auto, rôles, audit
