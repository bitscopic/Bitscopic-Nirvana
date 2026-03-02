#!/bin/bash
SAUTILS=/Users/ceisenhart/Bioinformatics/bitscopic_repos/Bitscopic-Nirvana/bin/Release/net10.0/SAUtils.dll
REF=/Users/ceisenhart/Bioinformatics/data/NIRVANA_DATA/20250123/nirvana/References/Homo_sapiens.GRCh37.Nirvana.dat
RCV=/Users/ceisenhart/Bioinformatics/data/NIRVANA_DATA/clinvar_update/ClinVarRCVRelease_2026-02.xml.gz
VCV=/Users/ceisenhart/Bioinformatics/data/NIRVANA_DATA/clinvar_update/ClinVarVCVRelease_2026-02.xml.gz
OUT=/Users/ceisenhart/Bioinformatics/data/NIRVANA_DATA/clinvar_update/

dotnet "$SAUTILS" clinvar --ref "$REF" --rcv "$RCV" --vcv "$VCV" --out "$OUT"
