# FixFox Backlog - 200 New Fixes, Scripts, and Feature Enhancements

This backlog is grounded in the current project shape:

- 213 existing fixes
- 21 existing categories
- 16 existing bundles
- existing but not yet surfaced services for duplicate files, installed programs, and scheduling

Each item is intentionally short so it can become a catalog entry, bundle, service task, or UI card.

## Network And Connectivity

1. [script] Export Wi-Fi diagnostics report with `netsh wlan show wlanreport`.
2. [script] Show packet loss by target over 60 seconds for router, DNS, and public IPs.
3. [script] Reset Winsock only, without full network reset.
4. [script] Re-enable disabled network adapters and show their status.
5. [script] Detect metered-connection settings and offer to disable them.
6. [guided] Walk users through forgetting and re-adding a broken Wi-Fi profile.
7. [script] Flush NCSI and captive portal state to fix "No Internet" false warnings.
8. [script] Show DHCP lease expiry and renewal timing details.
9. [script] Detect IPv6-only or broken IPv6 setups and suggest a safe fallback.
10. [bundle] Home internet recovery bundle: adapter reset, DNS flush, Wi-Fi profile refresh, and connectivity tests.

## DNS And Browsing

11. [script] Test DNS resolution speed across Cloudflare, Google, Quad9, and ISP DNS.
12. [script] Reset browser proxy settings for current user.
13. [script] Detect stale PAC script or auto-proxy config.
14. [script] Clear Chrome DNS cache and socket pools.
15. [script] Clear Edge DNS cache and socket pools.
16. [guided] Firefox safe-mode startup helper for extension conflict testing.
17. [script] Reset hosts file to Microsoft default while backing up the current file.
18. [script] Detect split-DNS issues on corporate VPN setups.
19. [script] Open browser certificate stores and common TLS repair steps.
20. [bundle] Browser recovery bundle: proxy reset, DNS tests, cache cleanup, and TLS sanity checks.

## Performance And Cleanup

21. [script] List top startup impact apps with publisher and path.
22. [script] Show top 20 scheduled tasks by trigger and vendor.
23. [script] Clean Delivery Optimization cache only.
24. [script] Clear DirectX shader cache.
25. [script] Clear Windows error report cache and temp dumps.
26. [script] Detect Storage Sense status and offer to enable it.
27. [script] Show largest folders under `%TEMP%`, `%LOCALAPPDATA%\Temp`, and Downloads.
28. [script] Enumerate RAM-heavy background apps by private memory.
29. [script] Detect low page-file configuration and suggest defaults.
30. [bundle] Performance tune-up bundle: temp cleanup, shader cleanup, Delivery Optimization cleanup, and startup review.

## Startup And Responsiveness

31. [script] Rebuild icon cache without clearing thumbnail cache.
32. [script] Rebuild thumbnail cache without clearing icon cache.
33. [script] Restart shell components individually: Explorer, StartMenuExperienceHost, SearchHost.
34. [script] Detect slow shell extensions from installed software.
35. [script] Show shell context menu handlers and non-Microsoft entries.
36. [guided] Safe boot clean-boot walkthrough for startup conflict isolation.
37. [script] Detect services stuck in starting or stopping state.
38. [script] Generate a boot diagnostics snapshot from event logs.
39. [script] Show recent app hang events by executable.
40. [bundle] Slow PC startup bundle: startup items, scheduled tasks, shell handlers, and hang events.

## Audio And Microphone

41. [script] Show all playback devices with default flags and state.
42. [script] Show all recording devices with levels and mute state where available.
43. [guided] Walk through app-level microphone permission repair by app.
44. [script] Reset Windows sonic/spatial audio settings.
45. [script] Detect exclusive-mode conflicts on playback devices.
46. [script] Export audio endpoint inventory for support review.
47. [script] Restart Bluetooth audio stack services.
48. [guided] Fix headset mic vs headphones profile confusion on Bluetooth devices.
49. [script] Detect mismatched default communication device vs default playback device.
50. [bundle] Meeting-ready audio bundle: mic check, communication device check, permissions, Bluetooth audio restart.

## Display And Graphics

51. [script] Show current resolution, scale, refresh rate, and HDR status for all monitors.
52. [script] Restart graphics stack with `Win+Ctrl+Shift+B` guidance and driver state checks.
53. [script] Detect mixed refresh-rate multi-monitor setups.
54. [script] Detect common HDR misconfiguration on SDR-only panels.
55. [guided] Walk through per-app GPU preference assignment.
56. [script] Enumerate monitor EDID names and active outputs.
57. [script] Check laptop hybrid GPU mode indicators where available.
58. [script] Export display driver versions and dates.
59. [script] Detect color-profile mismatches and list installed ICC profiles.
60. [bundle] Black screen and flicker bundle: display inventory, driver versions, refresh mismatch, cable/output guidance.

## Windows Update And Servicing

61. [script] Show failed Windows Update KBs from the last 30 days.
62. [script] Reset Delivery Optimization service only.
63. [script] Trigger update scan without full cache reset.
64. [script] Export CBS and Windows Update log bundle to Desktop.
65. [script] Detect pending reboot markers in registry and servicing stack.
66. [script] Detect broken optional features after update failures.
67. [guided] Roll back a problematic quality update with guardrails.
68. [guided] Roll back a problematic feature update with eligibility checks.
69. [script] Check Windows Recovery Environment status and enable it if disabled.
70. [bundle] Update recovery bundle: pending reboot check, logs export, service reset, update scan, SFC, DISM.

## Storage And File Integrity

71. [script] Detect OneDrive Files On-Demand hydration issues.
72. [script] Detect Controlled Folder Access blocks affecting file saves.
73. [script] Show SMART health summary for all physical disks.
74. [script] Detect TRIM support and last optimize status per volume.
75. [script] Find zero-byte files in selected folders.
76. [script] Find duplicate large files using the existing duplicate-file service.
77. [feature] Add a duplicate-file cleanup page powered by `DuplicateFileService`.
78. [script] Export large-folder report with top 50 directories by size.
79. [script] Validate common user-profile folders and recreate missing shell folders safely.
80. [bundle] Disk sanity bundle: SMART summary, free-space report, duplicate scan, large folders, temp cleanup.

## Security And Privacy

81. [script] Show Windows Firewall profiles, state, and active rules summary.
82. [script] Detect disabled SmartScreen and offer to restore defaults.
83. [script] Detect disabled Defender tamper protection and explain impact.
84. [script] List local admin accounts and their last logon time.
85. [script] Show recent RDP logons from event logs.
86. [script] Show recent failed sign-in attempts from security logs.
87. [script] Check BitLocker status for all volumes.
88. [guided] Walk through revoking app permissions for camera, mic, location, and notifications.
89. [script] Export Microsoft Defender threat history to a text report.
90. [bundle] Privacy hardening bundle: telemetry minimum, ad IDs off, app permission review, SmartScreen check.

## BSOD And Crash Recovery

91. [script] Export minidump inventory with file size and timestamps.
92. [script] Show recent bugcheck codes with friendly names from event logs.
93. [script] Detect verifier settings and offer safe reset.
94. [script] Show third-party drivers installed in the last 30 days.
95. [script] Detect overclocking utilities and common crash-linked tools.
96. [guided] Safe-mode crash triage flow with driver rollback suggestions.
97. [script] Export Reliability Monitor related events to a log file.
98. [script] Detect kernel memory dump settings and page-file incompatibilities.
99. [script] Check for `sfc` and `dism` output patterns that indicate storage corruption.
100. [bundle] Post-crash triage bundle: bugcheck export, dump inventory, recent drivers, SFC, DISM, memory-test guidance.

## Devices And Peripherals

101. [script] Detect USB hubs with power-saving enabled.
102. [script] Show recent USB disconnect/reconnect events.
103. [script] Detect missing HID devices and ghost devices in Device Manager.
104. [script] Restart Bluetooth support services and radio stack.
105. [script] Enumerate webcams and current privacy-policy state.
106. [guided] Pairing reset flow for Bluetooth keyboard, mouse, and headset.
107. [script] Detect missing game controller drivers and XInput devices.
108. [script] Show battery status for paired Bluetooth devices when available.
109. [script] Export device problem codes for all present PnP devices.
110. [bundle] USB recovery bundle: power settings, reconnect events, ghost devices, controller and webcam checks.

## Printers And Scanners

111. [script] Restart print spooler and clear only stuck queue jobs.
112. [script] Export installed printers, drivers, and port mappings.
113. [script] Detect WSD printers that should be converted to TCP/IP ports.
114. [guided] Walk through setting a default printer and disabling "let Windows manage default printer".
115. [script] Detect offline printer state and port connectivity.
116. [script] Remove stale printer drivers no longer tied to installed printers.
117. [script] Show scanner devices and WIA service state.
118. [script] Restart WIA service and common scan dependencies.
119. [guided] Network scanner reconnect flow for SMB and email scan targets.
120. [bundle] Printer rescue bundle: spooler reset, queue clear, port check, default-printer repair.

## App Repair And Dependencies

121. [script] Detect missing VC++ redistributables by installed runtime inventory.
122. [script] Detect missing DirectX legacy runtime components for older games.
123. [script] Detect missing .NET Desktop Runtime versions for installed apps.
124. [feature] Add an installed-programs page powered by `InstalledProgramsService`.
125. [script] Export app uninstall inventory with publisher, version, and install date.
126. [guided] Per-app reset/repair launcher for Store apps and classic apps.
127. [script] Re-register Windows installer service and verify service health.
128. [script] Detect broken app file associations for PDF, images, browser, and mailto.
129. [script] Reset common file associations to Windows defaults with confirmation.
130. [bundle] Broken app startup bundle: runtime checks, Store repair, app reset, file association review.

## Office, Email, And Cloud

131. [script] Clear Outlook profile cache temp files and autocomplete cache backup.
132. [guided] New Outlook vs classic Outlook profile repair path.
133. [script] Detect OneDrive sync client version and status.
134. [script] Restart OneDrive and trigger a health check.
135. [script] Detect Teams startup/install variant conflicts.
136. [guided] Walk through re-authenticating Microsoft 365 desktop apps.
137. [script] Detect OST/PST location, size, and disk pressure risks.
138. [script] Export Office click-to-run update channel and version.
139. [guided] Shared mailbox and cached-mode sanity checklist.
140. [bundle] Microsoft 365 recovery bundle: sign-in refresh, cache cleanup, OneDrive restart, Teams cleanup, version export.

## Remote Access And VPN

141. [script] Show active VPN adapters, routes, and DNS assignments.
142. [script] Detect split-tunnel route leaks after VPN disconnect.
143. [script] Restart RasMan and IKEEXT services.
144. [guided] Built-in Windows VPN profile recreation flow.
145. [script] Export RDP settings, saved targets, and recent connection history.
146. [script] Detect NLA and RDP firewall mismatches.
147. [script] Show remote assistance status and invitation policy.
148. [script] Detect AnyDesk, TeamViewer, and other remote-control autostart entries.
149. [guided] Corporate VPN troubleshooting checklist for DNS, MFA, and captive portals.
150. [bundle] Remote work recovery bundle: VPN services, DNS routes, RDP firewall, Teams audio, printer visibility.

## Power, Battery, And Thermals

151. [script] Export `powercfg /energy` report and open it automatically.
152. [script] Export `powercfg /sleepstudy` on supported devices.
153. [script] Detect modern standby support and related sleep blockers.
154. [script] Show active wake timers and last wake source.
155. [script] Detect devices allowed to wake the machine and let the user review them.
156. [script] Show current CPU power plan values for min/max processor state.
157. [script] Detect battery wear trends from battery report history.
158. [guided] Laptop overheating checklist with fan, vents, and surface guidance.
159. [script] Detect power throttling state for foreground and background apps.
160. [bundle] Battery saver bundle: power plan, wake timers, startup impact, screen and sleep defaults.

## Accounts And Sign-In

161. [script] Show all local users, admins, and password-last-set dates.
162. [script] Detect expired passwords and disabled accounts.
163. [guided] Microsoft account to local account migration checklist.
164. [guided] Windows Hello reset flow for PIN, face, and fingerprint.
165. [script] Clear cached credentials from Credential Manager with backup list.
166. [script] Show mapped drives and stale credential mappings.
167. [script] Detect profile temp-login situations and profile service errors.
168. [script] Export sign-in related event log entries from the last 7 days.
169. [guided] "Forgot PIN after motherboard change" recovery flow.
170. [bundle] Sign-in recovery bundle: Hello reset, credential cleanup, profile check, mapped-drive review.

## Gaming And Streaming

171. [script] Detect Xbox Game Bar, Game Mode, and capture settings in one report.
172. [script] Export GPU driver branch, shader cache status, and HAGS state.
173. [script] Detect overlays from Discord, Steam, NVIDIA, AMD, MSI Afterburner, and RivaTuner.
174. [guided] OBS scene-source performance audit checklist.
175. [script] Show DPC latency-related drivers from event history where available.
176. [script] Detect fullscreen optimizations status for selected game executables.
177. [guided] Fix controller input issues between Steam Input and native game input.
178. [script] Show storage throughput for the game drive and warn on low free space.
179. [bundle] Competitive gaming bundle: HAGS, overlays, power plan, network sanity, controller checks.
180. [bundle] Streaming bundle: OBS cache, audio sync checklist, upload path tests, browser source cleanup.

## Windows Features And Usability

181. [script] Reset Start menu pinned layout cache for current user.
182. [script] Reset taskbar search and widgets policies to Windows defaults.
183. [guided] File Explorer defaults reset with extensions, hidden files, and nav pane options.
184. [script] Detect broken default app associations after browser uninstall.
185. [script] Reset notification center and toast permissions by app.
186. [script] Toggle clipboard history and cloud clipboard settings safely.
187. [guided] Snap layouts and virtual desktop productivity setup.
188. [script] Export environment variables and user PATH problems.
189. [script] Detect stale shell links on Desktop and Start menu.
190. [bundle] Windows cleanup bundle: Explorer defaults, notifications, clipboard, Start and search refresh.

## FixFox Product Features And Automation

191. [feature] Add a dedicated diagnostics page for duplicate files.
192. [feature] Add a dedicated installed-programs page with sort, filter, and export.
193. [feature] Add a schedule page backed by `SchedulerService` for weekly bundles.
194. [feature] Add per-fix risk labels: safe, needs admin, may restart, advanced.
195. [feature] Add fix output export to text or Markdown.
196. [feature] Add a rollback notes field for fixes that change registry or power settings.
197. [feature] Add favorite fixes to the sidebar and command palette.
198. [feature] Add screenshot or report attachments to history entries.
199. [feature] Add CI smoke test for compiled WPF resources and icon references.
200. [bundle] Add first-run onboarding bundle that adapts to laptop, gaming PC, work PC, or low-end PC.
