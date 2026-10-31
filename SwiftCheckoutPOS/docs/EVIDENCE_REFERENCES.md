# GitHub evidence references

Documentation-only evidence companion · 2026-10-31 (Asia/Dhaka). The BackupAdmin source view is now tracked: its citations point directly to the real file. Only historical generated NuGet asset evidence remains routed here because the generated target is not tracked. The archived snapshot below is retained as provenance, not the current source authority; audit findings are not reclassified. Plain line numbers after links refer to the **original named source file**, not to this page's Markdown lines.

## BackupAdmin view snapshot

**superseded — file is now tracked** (2026-10-31 CQ merge). Current source: [SwiftCheckoutPOS.UI/Views/BackupAdmin/Index.cshtml](../SwiftCheckoutPOS.UI/Views/BackupAdmin/Index.cshtml), lines 1–119. Git ls-files confirms the real file is tracked; current ignore checks find no ignored authored source. All current citations use that file. The original numbered inert snapshot is retained below for historical provenance only; it is not a replacement Razor file and cannot execute.

```text
1: <!-- Project: SwiftCheckoutPOS.UI -->
2: <!-- File: Views/BackupAdmin/Index.cshtml -->
3: @model IEnumerable<SwiftCheckoutPOS.BLL.DTOs.BackupFileDTO>
4: @using SwiftCheckoutPOS.BLL.DTOs
5: 
6: @{
7:     ViewData["Title"] = "System Backup & Recovery";
8:     var config = ViewBag.Config as BackupConfigDTO;
9: }
10: 
11: <div class="d-flex justify-content-between align-items-center mb-4">
12:     <h2 class="fw-bold text-dark m-0">Database Backup & Recovery</h2>
13:     <form asp-action="TriggerManualBackup" method="post" class="m-0">
14:         <button type="submit" class="btn btn-warning fw-bold px-4 shadow-sm text-dark">
15:             <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="me-1"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
16:             Trigger Manual Backup Now
17:         </button>
18:     </form>
19: </div>
20: 
21: @if (TempData["Success"] != null)
22: {
23:     <div class="alert alert-success fw-bold shadow-sm border-0 py-2">@TempData["Success"]</div>
24: }
25: @if (TempData["Error"] != null)
26: {
27:     <div class="alert alert-danger fw-bold shadow-sm border-0 py-2">@TempData["Error"]</div>
28: }
29: 
30: <div class="row g-4 mb-4">
31:     <div class="col-lg-5">
32:         <div class="card shadow-sm border-0 rounded-3 h-100">
33:             <div class="card-header bg-dark text-white py-3">
34:                 <h6 class="mb-0 fw-bold">Automation Configuration</h6>
35:             </div>
36:             <div class="card-body p-4 bg-light">
37:                 <form asp-action="SaveConfiguration" method="post" id="configForm">
38: 
39:                     <div class="mb-4">
40:                         <label class="form-label fw-bold text-dark">Local Target Directory</label>
41:                         <input type="text" name="TargetDirectory" class="form-control font-monospace border-secondary shadow-sm" value="@config.TargetDirectory" />
42:                         <small class="text-muted mt-1 d-block">Physical hard drive path where .bak files are stored.</small>
43:                     </div>
44: 
45:                     <div class="mb-4">
46:                         <label class="form-label fw-bold text-dark mb-2">Autonomous Scheduling</label>
47:                         <div class="d-flex gap-4 p-3 bg-white border rounded shadow-sm">
48:                             <div class="form-check">
49:                                 <input class="form-check-input" type="radio" name="IsAutoBackupEnabled" id="autoOn" value="true" @(config.IsAutoBackupEnabled ? "checked" : "")>
50:                                 <label class="form-check-label fw-bold text-success" for="autoOn">Active</label>
51:                             </div>
52:                             <div class="form-check">
53:                                 <input class="form-check-input" type="radio" name="IsAutoBackupEnabled" id="autoOff" value="false" @(!config.IsAutoBackupEnabled ? "checked" : "")>
54:                                 <label class="form-check-label fw-bold text-danger" for="autoOff">Inactive</label>
55:                             </div>
56:                         </div>
57:                     </div>
58: 
59:                     <div class="mb-4">
60:                         <label class="form-label fw-bold text-dark">Execution Time (Daily)</label>
61:                         <input type="time" name="AutoBackupTime" class="form-control border-secondary shadow-sm w-50" value="@config.AutoBackupTime.ToString(@"hh\:mm")" />
62:                     </div>
63: 
64:                     <button type="submit" class="btn btn-primary fw-bold w-100 shadow-sm mt-2">Save Configuration (ENTER)</button>
65:                 </form>
66:             </div>
67:         </div>
68:     </div>
69: 
70:     <div class="col-lg-7">
71:         <div class="card shadow-sm border-0 rounded-3 h-100">
72:             <div class="card-header bg-dark text-white py-3">
73:                 <h6 class="mb-0 fw-bold">Local Backup History</h6>
74:             </div>
75:             <div class="card-body p-0">
76:                 <div class="table-responsive" style="max-height: 450px; overflow-y: auto;">
77:                     <table class="table table-hover mb-0 align-middle">
78:                         <thead class="table-light sticky-top">
79:                             <tr>
80:                                 <th class="py-3 ps-4">Timestamp</th>
81:                                 <th class="py-3">File Name</th>
82:                                 <th class="py-3 text-end">Size</th>
83:                                 <th class="py-3 text-center pe-4">Action</th>
84:                             </tr>
85:                         </thead>
86:                         <tbody class="bg-white">
87:                             @if (!Model.Any())
88:                             {
89:                                 <tr><td colspan="4" class="text-center py-5 text-muted fw-bold">No backups found in target directory.</td></tr>
90:                             }
91:                             else
92:                             {
93:                                 foreach (var file in Model)
94:                                 {
95:                                     <tr>
96:                                         <td class="ps-4 fw-bold text-muted" style="font-size: 0.9rem;">@file.CreationTime.ToString("yyyy-MM-dd HH:mm")</td>
97:                                         <td class="font-monospace text-dark" style="font-size: 0.85rem;">@file.FileName</td>
98:                                         <td class="text-end fw-bold">@file.FileSizeMB MB</td>
99:                                         <td class="text-center pe-4">
100:                                             <a asp-action="DownloadBackup" asp-route-fileName="@file.FileName" class="btn btn-sm btn-outline-primary px-3 fw-bold">Download</a>
101:                                         </td>
102:                                     </tr>
103:                                 }
104:                             }
105:                         </tbody>
106:                     </table>
107:                 </div>
108:             </div>
109:         </div>
110:     </div>
111: </div>
112: 
113: <script>
114:     document.addEventListener('keydown', function(event) {
115:         if (event.key === 'Enter' && event.target.tagName !== 'TEXTAREA') {
116:             document.getElementById('configForm').submit();
117:         }
118:     });
119: </script>
```

## Generated NuGet asset evidence

Original generated target: SwiftCheckoutPOS.DAL/obj/project.assets.json. It is not a tracked GitHub artifact. **The full historical generated file is not recreated or claimed available here.** The following are preserved historical audit quotations/provenance, not a current scan. See the [historical audit assessment](PROJECT_AUDIT.md#12-nuget-vulnerabilities) and the [current package task/status](DEVELOPMENT_ROADMAP.md#nuget-status--imm-11--playbook-t11).

The original asset line references in those quotations apply to the generated file at audit time. Package declarations have since changed to 10.0.14; never substitute today's generated asset bytes as proof of the older findings. The current owner-run reports are [nuget-vulnerable.txt](nuget-vulnerable.txt) and [nuget-outdated.txt](nuget-outdated.txt).

> - SwiftCheckoutPOS.DAL/SwiftCheckoutPOS.DAL.csproj:10–18 — **MSBuild/NuGet package declarations (not a C# method)**: `<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.9" />`
> - SwiftCheckoutPOS.DAL/obj/project.assets.json:1217–1220 — **NuGet ResolvePackageAssets target**: `"System.Security.Cryptography.Pkcs/9.0.4": { "type": "package", ... }`
> - SwiftCheckoutPOS.DAL/obj/project.assets.json:4035–4048 — **NuGet audit log in resolved assets**: `"code": "NU1902" ... "code": "NU1903"`

> - SwiftCheckoutPOS.DAL/SwiftCheckoutPOS.DAL.csproj:14 — **NuGet PackageReference (no C# method)**: `<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.9" />`
> - SwiftCheckoutPOS.DAL/obj/project.assets.json:23–31 — **NuGet resolved net10.0 target**: `"Azure.Identity/1.14.2": { ... "Microsoft.Identity.Client": "4.73.1"`
> - SwiftCheckoutPOS.DAL/obj/project.assets.json:814–821 — **NuGet resolved net10.0 target**: `"Microsoft.Identity.Client/4.73.1": {`

> The existing local target graph is supported by SwiftCheckoutPOS.DAL/obj/project.assets.json:594–609, SwiftCheckoutPOS.DAL/obj/project.assets.json:414–431 and SwiftCheckoutPOS.DAL/obj/project.assets.json:23–31:
