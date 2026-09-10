# Updating Nirvana ClinVar Supplementary Annotation

This document describes how to build an updated ClinVar NSA (Nirvana Supplementary Annotation) database from the latest ClinVar XML release files.

## Prerequisites

- .NET SDK 10.0+ installed (`brew install --cask dotnet-sdk`)
- The Bitscopic-Nirvana repository checked out and built
- A Nirvana reference sequence file for each assembly you want to build:
  - GRCh37: `Homo_sapiens.GRCh37.Nirvana.dat`
  - GRCh38: `Homo_sapiens.GRCh38.Nirvana.dat`
- ~15GB of free disk space for the ClinVar XML files and output

## 1. Build SAUtils

From the Bitscopic-Nirvana repo root:

```
dotnet build SAUtils/SAUtils.csproj -c Release
```

The output DLL will be at `bin/Release/net10.0/SAUtils.dll`.

Note: The csproj files were updated from `net8.0` to `net10.0` to be compatible with the .NET 10 SDK. If you have .NET 8 installed, you can revert this.

## 2. Download ClinVar XML Files

ClinVar publishes monthly XML releases. You need two files:

- **RCV file** (clinical assertions): `https://ftp.ncbi.nlm.nih.gov/pub/clinvar/xml/RCV_release/`
- **VCV file** (variant-level aggregation): `https://ftp.ncbi.nlm.nih.gov/pub/clinvar/xml/`

For example, to get the February 2026 release:

```
wget https://ftp.ncbi.nlm.nih.gov/pub/clinvar/xml/RCV_release/ClinVarRCVRelease_2026-02.xml.gz
wget https://ftp.ncbi.nlm.nih.gov/pub/clinvar/xml/ClinVarVCVRelease_2026-02.xml.gz
```

These files are large (~5GB each). The `*-latest.xml.gz` symlinks always point to the most recent release.

## 3. Create a Version File

SAUtils requires a `.version` file alongside the RCV file. Create a file named `<RCV_filename>.version` with the following format:

```
NAME=ClinVar
VERSION=2026-02
DATE=2026-02-24
DESCRIPTION=
```

The `VERSION` field is used to name the output files (e.g. `ClinVar_2026-02.nsa`). The `DATE` field should be the date you are generating the database.

## 4. Run the ClinVar Build

Run SAUtils with the `clinvar` subcommand. The general form is:

```
dotnet <path_to>/SAUtils.dll clinvar --ref <reference.dat> --rcv <RCV.xml.gz> --vcv <VCV.xml.gz> --out <output_dir>
```

You need to run this once per assembly, with the appropriate reference file. Use separate output directories to avoid overwriting (the output filename is the same regardless of assembly).

Convenience scripts are provided in this directory:

- `build_clinvar.sh` — GRCh37 build
- `build_clinvar_hg38.sh` — GRCh38 build

Each produces three output files:

- `ClinVar_2026-02.nsa` (~130MB) - the binary annotation database
- `ClinVar_2026-02.nsa.idx` - the index for random access
- `ClinVar_2026-02.nsa.schema` - the JSON schema definition

### Estimated Run Times (MacBook Pro, February 2026 release)

| Assembly | Run Time | Output Size |
|----------|----------|-------------|
| GRCh37   | ~52 min  | 130MB       |
| GRCh38   | ~59 min  | 129MB       |

The majority of the time is spent parsing the VCV XML file (~5GB). The VCV parsing does not produce any console output, so it will appear to hang for 15-20 minutes before printing "Found X VCV records". This is normal.

## 5. Deploy

Copy the three output files into your Nirvana SupplementaryAnnotation directory, replacing the existing ClinVar `.nsa`, `.nsa.idx`, and `.nsa.schema` files. Keep the GRCh37 and GRCh38 outputs separate — they go into their respective assembly's annotation directory.

## Code Changes for ClinVar XML Schema v2.x

The ClinVar XML schema changed significantly between the version Nirvana 3.18 was written for and the current releases (VCV schema 2.5, RCV schema 2.2). The following code changes were made to handle the new format:

### XML Tag Renames

| Old Tag | New Tag |
|---|---|
| `InterpretedRecord` | `ClassifiedRecord` |
| `Interpretations` | `Classifications` |
| `Interpretation` (with `Type="Clinical significance"`) | `GermlineClassification` / `SomaticClinicalImpact` / `OncogenicityClassification` |
| `ClinicalSignificance` (in RCV) | `Classifications > GermlineClassification` |

### New Review Statuses

The following review status strings were added to `ClinVarCommon.cs`:

- `criteria provided, conflicting classifications` (maps to `conflicting_interpretations`)
- `no classification provided` (maps to `no_assertion`)
- `criteria provided, multiple submitters` (maps to `multiple_submitters`)
- `no classification for the single variant` (maps to `no_interpretation_single`)

### New Clinical Significance Values

The following significance values were added to `ClinVarCommon.cs`:

- `likely oncogenic`, `oncogenic`
- `tier i - strong`, `tier ii - potential`, `tier iii - unknown`, `tier iv - benign`
- `vus-mid`, `vus-high`, `vus-low`
- `no classification for the single variant`

### Files Modified

- `SAUtils/InputFileParsers/ClinVar/ClinVarVariationReader.cs` - Rewrote to handle both old (`InterpretedRecord`) and new (`ClassifiedRecord`) VCV XML schemas
- `SAUtils/InputFileParsers/ClinVar/ClinVarParser.cs` - Added `GetClinicalSignificanceFromClassifications()` for new RCV schema; null safety for `_reviewStatus`
- `SAUtils/InputFileParsers/ClinVar/ClinVarCommon.cs` - Added new review statuses and pathogenicity values
- `SAUtils/CreateClinvarDb/ClinVarStats.cs` - Added null safety for `Significances` and `Id` fields

### Other Changes

- A VCV record can now have both `InterpretedRecord` and `IncludedRecord` simultaneously. The old code threw an exception via XOR check; the new code prefers `ClassifiedRecord` > `InterpretedRecord` > `IncludedRecord`.
- Unknown review statuses and clinical significances now log warnings instead of throwing exceptions, so a single unknown value doesn't abort the entire build.

## February 2026 Build Statistics

- 4,128,548 VCV records parsed
- 242,239 RCV records had VCV IDs not found in the VCV file (likely very new entries)
- Build time: ~52 minutes
