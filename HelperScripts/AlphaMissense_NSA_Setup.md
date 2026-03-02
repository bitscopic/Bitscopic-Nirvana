# Creating an AlphaMissense Supplementary Annotation (.nsa) for Nirvana

This guide documents how to convert the AlphaMissense TSV into a Nirvana
supplementary annotation (.nsa) file using SAUtils. The process is the same
for both GRCh37 (hg19) and GRCh38 (hg38) — only the source file, SAUtils
header, and Nirvana reference differ.

## Overview

AlphaMissense (DeepMind, 2023) provides pathogenicity scores for all possible
single amino acid substitutions (~71M variants). The original TSV contains
multiple rows per genomic position (one per transcript). Nirvana's SAUtils
expects one row per allele, so we first canonicalize the file by selecting a
single transcript per variant, then convert it to .nsa format.

## Source data

DeepMind publishes separate files for each genome build:

| Build  | Download URL | Size |
|--------|-------------|------|
| hg19   | `https://storage.googleapis.com/dm_alphamissense/AlphaMissense_hg19.tsv.gz` | ~593 MB |
| hg38   | `https://storage.googleapis.com/dm_alphamissense/AlphaMissense_hg38.tsv.gz` | ~613 MB |

Both files have the same 10-column format:
```
#CHROM  POS  REF  ALT  genome  uniprot_id  transcript_id  protein_variant  am_pathogenicity  am_class
```

The `genome` column is either `hg19` or `hg38` depending on the file.

## Prerequisites

- Python 3
- Nirvana SAUtils binary (built from this repo via `dotnet build SAUtils/SAUtils.csproj -c Release`)
- .NET runtime
- A Nirvana reference file for the target assembly
- A canonical Ensembl transcript list (one transcript ID per line, with or without version numbers)

## Quick start (hg38)

A build script is provided at `~/vep_data/build_alphamissense_hg38.sh` that
runs the full pipeline. Place `AlphaMissense_hg38.tsv.gz` in `~/vep_data/`
and run:

```bash
~/vep_data/build_alphamissense_hg38.sh
```

The script handles all steps below automatically, with skip logic for steps
whose output already exists.

## Step-by-step process

### Step 1: Download and decompress

```bash
wget https://storage.googleapis.com/dm_alphamissense/AlphaMissense_hg38.tsv.gz
gzcat AlphaMissense_hg38.tsv.gz > AlphaMissense_hg38.tsv
```

For hg19, substitute `hg38` with `hg19` in the URLs and filenames.

### Step 2: Get canonical Ensembl transcripts

SAUtils requires one row per (CHROM, POS, REF, ALT). The AlphaMissense file has
multiple rows per variant (one per overlapping transcript). We resolve this by
preferring canonical Ensembl transcripts.

Download the canonical transcript list from Ensembl BioMart or extract from
VEP cache. The file should contain one Ensembl transcript ID per line:
```
ENST00000641515.2
ENST00000426406.1
ENST00000332831.5
...
```

### Step 3: Canonicalize the AlphaMissense TSV

Run the canonicalization script (`canonicalize_alphamissense.py` in this
directory). The input/output filenames are hardcoded in `__main__` — edit them
for your target assembly:

```python
main("AlphaMissense_hg38.tsv", "canonical_ensemble_transcripts.txt", "AlphaMissense_hg38_Filtered.tsv")
```

The script:
1. Skips comment/header lines from the source file
2. Groups rows by (CHROM, POS, REF, ALT)
3. For each group, selects the row matching a canonical Ensembl transcript
4. Falls back to alphabetical transcript sort if no canonical match
5. Outputs a TSV with 6 columns: `CHROM, POS, REF, ALT, Transcript, AM_score`
   (extracted from columns 0-3, 6, and 8 of the original 10-column file)

### Step 4: Add the SAUtils header

Prepend the appropriate header file for your assembly:

- **GRCh37:** `alphamissense_sautils_header.txt` (contains `#assembly=GRCh37`)
- **GRCh38:** `alphamissense_sautils_header_hg38.txt` (contains `#assembly=GRCh38`)

```bash
cat alphamissense_sautils_header_hg38.txt AlphaMissense_hg38_Filtered.tsv > AlphaMissense_hg38.nirvana.tsv
```

The header defines:
```
#title=AlphaMissense
#assembly=GRCh38
#matchVariantsBy=allele
#CHROM	POS	REF	ALT	Transcript	AM_score
#categories	.	.	.	.	Score
#descriptions	.	.	.	.	.
#type	.	.	.	string	number
```

- `matchVariantsBy=allele` means Nirvana matches on CHROM+POS+REF+ALT
- `Transcript` is typed as `string`, `AM_score` as `number` (with category `Score`)

### Step 5: Convert to .nsa with SAUtils

```bash
dotnet /path/to/SAUtils.dll customvar \
  --ref /path/to/References/Homo_sapiens.GRCh38.Nirvana.dat \
  --in AlphaMissense_hg38.nirvana.tsv \
  --out .
```

This produces three files:
```
AlphaMissense_hg38.nirvana.nsa          # ~317 MB, the binary annotation data
AlphaMissense_hg38.nirvana.nsa.idx      # ~11 KB, the index
AlphaMissense_hg38.nirvana.nsa.schema   # ~362 B, JSON schema
```

For hg19, the NSA is ~314 MB.

### Step 6: Install into Nirvana

Copy the three .nsa files into your Nirvana supplementary annotation directory:
```bash
cp AlphaMissense_hg38.nirvana.nsa* /path/to/SupplementaryAnnotation/GRCh38/
```

Nirvana will automatically pick up the new annotation.

## Output in Nirvana JSON

After installation, Nirvana JSON output will include AlphaMissense annotations:
```json
{
  "AlphaMissense": {
    "refAllele": "G",
    "altAllele": "T",
    "Transcript": "ENST00000335137.4",
    "AM_score": 0.2937
  }
}
```

## Files in this directory

- `canonicalize_alphamissense.py` — Deduplication script (picks canonical transcript per variant, outputs 6-column TSV)
- `alphamissense_sautils_header.txt` — SAUtils header for GRCh37/hg19
- `alphamissense_sautils_header_hg38.txt` — SAUtils header for GRCh38/hg38
