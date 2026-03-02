using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using IO;

namespace SAUtils.InputFileParsers.ClinVar
{
    public sealed class ClinVarVariationReader : IDisposable
    {
        private const string VcvRecordTag      = "VariationArchive";
        private const string AccessionTag      = "Accession";
        private const string VersionTag        = "Version";
        private const string DateTag           = "DateLastUpdated";
        private const string ReviewStatusTag   = "ReviewStatus";

        // Old XML schema tags
        private const string InterpretedRecordTag = "InterpretedRecord";
        private const string InterpretationsTag   = "Interpretations";
        private const string InterpretationTag    = "Interpretation";

        // New XML schema tags (ClinVar VCV 2.x)
        private const string ClassifiedRecordTag       = "ClassifiedRecord";
        private const string ClassificationsTag        = "Classifications";
        private const string GermlineClassificationTag = "GermlineClassification";
        private const string SomaticClinicalImpactTag  = "SomaticClinicalImpact";
        private const string OncogenicityTag           = "OncogenicityClassification";

        private const string IncludedRecordTag = "IncludedRecord";

        private const string DescriptionTag = "Description";
        private const string ExplanationTag = "Explanation";
        private const string TypeTag        = "Type";


        private readonly Stream _readStream;

        public ClinVarVariationReader(Stream readStream)
        {
            _readStream = readStream;
        }

        public IEnumerable<VcvItem> GetItems()
        {
            using (var reader = FileUtilities.GetStreamReader(_readStream))
            using (var xmlReader = XmlReader.Create(reader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreWhitespace = true}))
            {
                xmlReader.ReadToDescendant(VcvRecordTag);
                do
                {
                    var  subTreeReader = xmlReader.ReadSubtree();
                    var xElement       = XElement.Load(subTreeReader);

                    var item = ExtractVariantRecord(xElement);

                    if (item == null) continue;
                    yield return item;

                } while (xmlReader.ReadToNextSibling(VcvRecordTag));
            }
        }

        private static VcvItem ExtractVariantRecord(XElement xElement)
        {
            if (xElement == null || xElement.IsEmpty) return null;

            var accession  = xElement.Attribute(AccessionTag)?.Value;
            var version    = xElement.Attribute(VersionTag)?.Value;
            var dateString = xElement.Attribute(DateTag)?.Value;
            var date       = ClinVarParser.ParseDate(dateString);

            // Try new schema first (ClassifiedRecord), then old schema (InterpretedRecord)
            var classifiedRecord    = xElement.Element(ClassifiedRecordTag);
            var interpretationRecord = xElement.Element(InterpretedRecordTag);
            var includedRecord      = xElement.Element(IncludedRecordTag);

            // New schema: ClassifiedRecord
            if (classifiedRecord != null && !classifiedRecord.IsEmpty)
            {
                return ExtractFromClassifiedRecord(classifiedRecord, accession, version, date);
            }

            // Old schema: InterpretedRecord
            if (interpretationRecord != null && !interpretationRecord.IsEmpty)
            {
                return ExtractFromInterpretedRecord(interpretationRecord, accession, version, date);
            }

            // Fallback: IncludedRecord
            if (includedRecord == null || includedRecord.IsEmpty) return null;

            // IncludedRecord can use either old or new schema internally
            return ExtractFromClassifiedOrInterpretedRecord(includedRecord, accession, version, date);
        }

        private static VcvItem ExtractFromClassifiedRecord(XElement record, string accession, string version, long date)
        {
            var classifications = record.Element(ClassificationsTag);
            var significances = GetSignificancesFromClassifications(classifications);

            var reviewStatusString = GetReviewStatusFromClassifications(classifications)
                                     ?? record.Element(ReviewStatusTag)?.Value;

            if (reviewStatusString == null) return null;

            if (!ClinVarCommon.ReviewStatusNameMapping.TryGetValue(reviewStatusString, out var reviewStatus))
            {
                Console.WriteLine($"WARNING: Unknown review status '{reviewStatusString}' for {accession}.{version}. Skipping.");
                return null;
            }
            return new VcvItem(accession, version, date, reviewStatus, significances);
        }

        private static VcvItem ExtractFromInterpretedRecord(XElement record, string accession, string version, long date)
        {
            var significances = GetSignificancesFromInterpretations(record.Element(InterpretationsTag));

            var reviewStatusString = record.Element(ReviewStatusTag)?.Value;
            if (reviewStatusString == null) return null;

            if (!ClinVarCommon.ReviewStatusNameMapping.TryGetValue(reviewStatusString, out var reviewStatus))
            {
                Console.WriteLine($"WARNING: Unknown review status '{reviewStatusString}' for {accession}.{version}. Skipping.");
                return null;
            }
            return new VcvItem(accession, version, date, reviewStatus, significances);
        }

        private static VcvItem ExtractFromClassifiedOrInterpretedRecord(XElement record, string accession, string version, long date)
        {
            // Try new schema tags first
            var classifications = record.Element(ClassificationsTag);
            if (classifications != null)
                return ExtractFromClassifiedRecord(record, accession, version, date);

            // Fall back to old schema
            var interpretations = record.Element(InterpretationsTag);
            if (interpretations != null)
                return ExtractFromInterpretedRecord(record, accession, version, date);

            return null;
        }

        private static string GetReviewStatusFromClassifications(XElement classifications)
        {
            if (classifications == null || classifications.IsEmpty) return null;

            // Check GermlineClassification, SomaticClinicalImpact, OncogenicityClassification
            foreach (var tagName in new[] { GermlineClassificationTag, SomaticClinicalImpactTag, OncogenicityTag })
            {
                var element = classifications.Element(tagName);
                var reviewStatus = element?.Element(ReviewStatusTag)?.Value;
                if (reviewStatus != null) return reviewStatus;
            }
            return null;
        }

        private static List<string> GetSignificancesFromClassifications(XElement classifications)
        {
            if (classifications == null || classifications.IsEmpty) return null;

            var significanceList = new List<string>();

            // Check GermlineClassification, SomaticClinicalImpact, OncogenicityClassification
            foreach (var tagName in new[] { GermlineClassificationTag, SomaticClinicalImpactTag, OncogenicityTag })
            {
                var element = classifications.Element(tagName);
                if (element == null) continue;

                var description = element.Element(DescriptionTag)?.Value?.ToLower();
                var explanation = element.Element(ExplanationTag)?.Value?.ToLower();
                if (description == null && explanation == null) continue;

                var significances = ClinVarCommon.GetSignificances(description, explanation);
                foreach (var significance in significances)
                {
                    if (!ClinVarCommon.ValidPathogenicity.Contains(significance))
                    {
                        Console.WriteLine($"WARNING: Unknown clinical significance '{significance}'. Skipping.");
                        continue;
                    }
                    significanceList.Add(significance);
                }
            }
            return significanceList.Count > 0 ? significanceList : null;
        }

        private static List<string> GetSignificancesFromInterpretations(XElement interpretations)
        {
            if (interpretations == null || interpretations.IsEmpty) return null;

            var significanceList = new List<string>();
            foreach (var interpretation in interpretations.Elements(InterpretationTag))
            {
                var type = interpretation.Attribute(TypeTag)?.Value;
                if(type==null || type != "Clinical significance") continue;

                var description = interpretation.Element(DescriptionTag)?.Value?.ToLower();
                var explanation = interpretation.Element(ExplanationTag)?.Value?.ToLower();
                if(description == null && explanation == null) continue;

                var significances = ClinVarCommon.GetSignificances(description, explanation);
                foreach (var significance in significances)
                {
                    if (!ClinVarCommon.ValidPathogenicity.Contains(significance))
                    {
                        Console.WriteLine($"WARNING: Unknown clinical significance '{significance}'. Skipping.");
                        continue;
                    }
                    significanceList.Add(significance);
                }
            }
            return significanceList.Count > 0 ? significanceList : null;
        }

        public void Dispose()
        {
            _readStream?.Dispose();
        }
    }
}
