using IgCodegen;

string resourcesDir = args.Length > 0 ? args[0] : "../../../../fhir/ig/fsh-generated/resources";
string outputDir = args.Length > 1 ? args[1] : "../src";

var scanner = new IgPackageScanner();
var model = scanner.Scan(resourcesDir, "io.github.miracum.recruit", "0.1.0");
string generated = CSharpConstantsGenerator.WriteTo(model, "RecruIT.FhirConstants", "Recruit", outputDir);
Console.WriteLine($"Generated {generated}");
