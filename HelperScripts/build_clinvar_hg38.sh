#!/bin/bash
SAUTILS=/Users/ceisenhart/Bioinformatics/bitscopic_repos/Bitscopic-Nirvana/bin/Release/net10.0/SAUtils.dll
REF=/Users/ceisenhart/Bioinformatics/data/NIRVANA_DATA/20250530/References/Homo_sapiens.GRCh38.Nirvana.dat
RCV=/Users/ceisenhart/Bioinformatics/data/NIRVANA_DATA/clinvar_update/ClinVarRCVRelease_2026-02.xml.gz
VCV=/Users/ceisenhart/Bioinformatics/data/NIRVANA_DATA/clinvar_update/ClinVarVCVRelease_2026-02.xml.gz
OUT=/Users/ceisenhart/Bioinformatics/data/NIRVANA_DATA/clinvar_update/GRCh38/

dotnet "$SAUTILS" clinvar --ref "$REF" --rcv "$RCV" --vcv "$VCV" --out "$OUT"
