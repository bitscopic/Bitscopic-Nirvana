import sys
from collections import defaultdict

def strip_version(transcript_id):
    return transcript_id.split('.')[0]

def resolve_variants_at_position(pos_rows, canonical_set, fallback_tracker):
    """
    Groups rows by (REF, ALT) and picks one transcript for each.
    """
    # Map (REF, ALT) -> list of rows
    allele_groups = defaultdict(list)
    for row in pos_rows:
        allele_groups[(row[2], row[3])].append(row)
    
    results = []
    for (ref, alt), rows in allele_groups.items():
        # 1. Try for Canonical
        canonical_matches = [r for r in rows if strip_version(r[6]) in canonical_set]
        
        if canonical_matches:
            canonical_matches.sort(key=lambda x: x[6]) # Deterministic tie-break
            results.append(canonical_matches[0])
        else:
            # 2. Fallback: Sort alphabetically and pick first
            rows.sort(key=lambda x: x[6])
            if len(rows) > 1:
                fallback_tracker['count'] += 1
            results.append(rows[0])
            
    return results

def main(am_file, canonical_file, output_file):
    print(f"Loading canonical transcripts from {canonical_file}...")
    with open(canonical_file, 'r') as f:
        canonical_set = {strip_version(line.strip()) for line in f if line.strip()}

    fallback_tracker = {'count': 0}
    current_pos_rows = []
    current_pos_key = None
    
    with open(am_file, 'r') as f_in, open(output_file, 'w') as f_out:
        for line in f_in:
            if line.startswith('#'):
                continue

            cols = line.strip().split('\t')
            # Group ONLY by Chrom and Position
            pos_key = (cols[0], cols[1])

            if pos_key == current_pos_key:
                current_pos_rows.append(cols)
            else:
                if current_pos_rows:
                    winners = resolve_variants_at_position(current_pos_rows, canonical_set, fallback_tracker)
                    for w in winners:
                        # Output: CHROM, POS, REF, ALT, transcript_id, am_pathogenicity
                        f_out.write('\t'.join([w[0], w[1], w[2], w[3], w[6], w[8]]) + '\n')

                current_pos_key = pos_key
                current_pos_rows = [cols]

        # Handle last position
        if current_pos_rows:
            winners = resolve_variants_at_position(current_pos_rows, canonical_set, fallback_tracker)
            for w in winners:
                f_out.write('\t'.join([w[0], w[1], w[2], w[3], w[6], w[8]]) + '\n')

    print("-" * 30)
    print(f"DONE. Output: {output_file}")
    print(f"Unique variants using fallback transcript: {fallback_tracker['count']}")

if __name__ == "__main__":
    main("AlphaMissense_hg19.tsv", "canonical_ensemble_transcripts.02.11.2026.txt", "AlphaMissense_hg19_Filtered.tsv")
