# ContosoDashboard

The ContosoDashboard application is intended for TRAINING PURPOSES ONLY.

The ContosoDashboard repository contains the starter code project for training that teaches Spec-Driven Development (SDD) using the GitHub Spec Kit. ContosoDashboard is a fictional application created solely for educational purposes.

- The project codebase is NOT intended for use in production environments.
- The project architecture is NOT intended as a model for production applications.
- The project is NOT actively maintained and may contain bugs or security vulnerabilities.
- The project is provided "as-is" without warranties or support of any kind.
- The project implements mock authentication and authorization for training purposes only.
- The project does NOT implement cloud integration or external service dependencies (local only and offline to maximize training availability).
- The project demonstrates good coding practices, simplified for a training context, with known and documented limitations.

## 🔒 Security Features (Training Implementation)

This application includes a **mock authentication system** designed for training without external dependencies:

- ✅ Cookie-based authentication (8-hour sliding expiration)
- ✅ Claims-based identity with user roles
- ✅ Razor Pages for login/logout (proper HTTP request handling)
- ✅ Custom authentication state provider for Blazor Server integration
- ✅ Authorization enforcement on all protected pages (`[Authorize]` attribute)
- ✅ Role-based access control (RBAC) with hierarchical permissions
- ✅ Service-level security to prevent unauthorized data access
- ✅ IDOR (Insecure Direct Object Reference) protection
- ✅ Defense in depth (middleware, page attributes, service checks)
- ✅ Security headers (CSP, X-Frame-Options, X-XSS-Protection, etc.)
- ✅ Cookie security with sliding expiration
- ✅ User isolation - each user sees only their authorized data
- ✅ No external services required (suitable for offline training)

**Note**: The mock authentication is suitable for training only. Production deployments require proper identity providers with password hashing, MFA, OAuth 2.0/OpenID Connect, and compliance with security standards (WCAG 2.1, TLS encryption, audit logging).

### Mock Login System

**Available Users** (no password required - select from dropdown):

| Display Name | Email | Role | Department |
|-------------|-------|------|------------|
| System Administrator | `admin@contoso.com` | Administrator | IT |
| Camille Nicole | `camille.nicole@contoso.com` | Project Manager | Engineering |
| Floris Kregel | `floris.kregel@contoso.com` | Team Lead | Engineering |
| Ni Kang | `ni.kang@contoso.com` | Employee | Engineering |

**Login Process:**

1. Navigate to `/login` (automatic redirect if not authenticated)
2. Select a user from the dropdown
3. Click "Login" - you'll be redirected to the dashboard as that user

⚠️ **Important:** This mock authentication system is for **training only**. Production applications should use Azure AD, Identity Server, Auth0, or similar identity providers with proper password hashing, MFA, and OAuth 2.0/OpenID Connect.

## Overview

ContosoDashboard is built using ASP.NET Core 8.0 with Blazor Server and provides a centralized platform for:

- Task management and tracking
- Project oversight and collaboration
- Team coordination
- Notifications and announcements
- User profile management

## Features

### ✅ Implemented Features

- **Mock Authentication System**: User selection login, cookie-based auth, claims-based identity
- **Authorization Enforcement**: `[Authorize]` attributes on all protected pages, role-based policies
- **Dashboard Home Page**: Personalized dashboard with summary cards showing active tasks, due dates, projects, and notifications
- **Task Management**: View, filter, sort, and update tasks with priority levels and status tracking
- **Project Management**: Browse projects with completion percentages, team members, and status indicators
- **Project Details**: Comprehensive project view with task list, team members, and project statistics
- **Team Directory**: Browse team members by department with status, roles, and contact information
- **Notifications Center**: View and manage all notifications with read/unread status and priority badges
- **User Profile**: Update personal information, availability status, and notification preferences
- **Document Management (MVP)**: Upload files, view your documents with category filtering and date/title sorting. See [Documents](#documents) below.
- **Service-Level Security**: Authorization checks prevent IDOR vulnerabilities
- **Data Models**: Complete entity framework models for Users, Tasks, Projects, Notifications, and Announcements
- **Business Services**: Service layer for all core functionality (Tasks, Projects, Users, Notifications, Dashboard)
- **Database Context**: EF Core DbContext with relationships, indexes, and seed data

### 🔧 Technical Stack

- **Framework**: ASP.NET Core 10.0
- **UI**: Blazor Server
- **Database**: SQLite with Entity Framework Core
- **Authentication**: Cookie-based mock authentication for training (Azure AD/Microsoft Entra ID ready)
- **Authorization**: Claims-based identity with role-based access control
- **Styling**: Bootstrap 5.3 with Bootstrap Icons
- **Architecture**: Clean separation of concerns with Models, Services, Data, and Pages layers
- **Security**: IDOR protection, service-level authorization, `[Authorize]` attributes

## Architecture Principles

### Offline-First with Cloud Migration Path

This training application follows an **offline-first architecture** with abstraction layers that enable seamless migration to Azure services:

**Current Implementation (Training/Offline):**
- **Database**: SQLite (offline, file-based development database)
- **File Storage**: Local filesystem for any file-based features
- **Authentication**: Cookie-based mock authentication

**Production Migration Path:**
- **Database**: Azure SQL Database (replace connection string, no code changes)
- **File Storage**: Azure Blob Storage (swap `IFileStorageService` implementation)
- **Authentication**: Microsoft Entra ID (replace authentication middleware)

**Key Design Pattern - Infrastructure Abstraction:**

All infrastructure dependencies use **interface abstractions** to enable switching between local and cloud implementations:

```csharp
// Example: File storage abstraction
public interface IFileStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);
    Task DeleteAsync(string filePath);
    Task<Stream> DownloadAsync(string filePath);
}

// Training: LocalFileStorageService (uses System.IO)
// Production: AzureBlobStorageService (uses Azure SDK)
// Swap via dependency injection - no business logic changes required
```

**Benefits of This Approach:**
- Students learn proper abstraction patterns and dependency injection
- Training works offline without Azure subscriptions or cloud costs
- Migration to production requires only configuration and implementation swaps
- Business logic remains unchanged during cloud migration
- Demonstrates industry-standard separation of concerns

**File upload best practice:** When implementing file uploads, generate unique file paths (using GUID) before database insertion to prevent duplicate key violations and orphaned records.

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or Visual Studio Code

SQLite requires no separate database server installation; the database file is created automatically on first run.

### Quick Start

1. **Navigate to the project directory**:

   ```powershell
   cd ContosoDashboard
   ```

2. **Run the application** (database will be created automatically):

   ```powershell
   dotnet run
   ```

3. **Open your browser** to `http://localhost:5000`

4. **Login** - Select any user from the dropdown (no password required)

The application automatically creates and seeds the database on first run with sample users, projects, tasks, and announcements.

### Testing Security Features

#### Test 1: Authentication Required

- Open browser in incognito mode
- Try to navigate to `https://localhost:xxxx/tasks`
- Expected: Redirect to `/login`

#### Test 2: User Isolation

- Login as "Ni Kang"
- Note the tasks and projects shown
- Logout and login as "Floris Kregel"
- Expected: Different tasks and projects displayed

#### Test 3: IDOR Protection

- Login as "Ni Kang" and view a project (note the ID in URL)
- Logout and login as "System Administrator"
- Try to access the same project by URL
- Expected: Access only if you're a member

#### Test 4: Role-Based Features

- Login as different users to see varying levels of access
- Employee: View assigned tasks, update status
- Project Manager: Manage projects, assign tasks
- Administrator: Full system access

## Project Structure

```plaintext
ContosoDashboard/
├── Data/
│   └── ApplicationDbContext.cs      # EF Core database context
├── Models/
│   ├── User.cs                      # User entity with roles
│   ├── TaskItem.cs                  # Task entity
│   ├── Project.cs                   # Project entity
│   ├── TaskComment.cs               # Task comments
│   ├── Notification.cs              # User notifications
│   ├── ProjectMember.cs             # Project team members
│   └── Announcement.cs              # System announcements
├── Services/
│   ├── IUserService.cs / UserService.cs
│   ├── ITaskService.cs / TaskService.cs
│   ├── IProjectService.cs / ProjectService.cs
│   ├── INotificationService.cs / NotificationService.cs
│   ├── IDashboardService.cs / DashboardService.cs
│   └── CustomAuthenticationStateProvider.cs  # Blazor Server auth integration
├── Pages/
│   ├── Index.razor                  # Dashboard home page
│   ├── Login.cshtml / Login.cshtml.cs  # Mock authentication login (Razor Page)
│   ├── Logout.cshtml / Logout.cshtml.cs  # Logout handler (Razor Page)
│   ├── Tasks.razor                  # Task list and management
│   ├── Projects.razor               # Project list view
│   ├── ProjectDetails.razor         # Individual project details
│   ├── Team.razor                   # Team member directory
│   ├── Notifications.razor          # Notification center
│   ├── Profile.razor                # User profile page
│   └── _Host.cshtml                 # Blazor Server host page
├── Shared/
│   ├── MainLayout.razor             # Main layout template
│   └── NavMenu.razor                # Navigation sidebar
├── wwwroot/
│   └── css/site.css                 # Custom styles
├── Program.cs                       # Application entry point
├── appsettings.json                 # Configuration
└── ContosoDashboard.csproj          # Project file
```

## Configuration

### Database Connection

The default connection string in `appsettings.json` uses SQLite:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=ContosoDashboard.db"
}
```

The `ContosoDashboard.db` file is created in the project directory on first run. Update the `Data Source` value to store it elsewhere.

### Production Authentication Guidance

This training application uses mock authentication. For production applications, you would need to:

- Implement proper identity providers (Azure AD, Identity Server, Auth0)
- Add password hashing and salting (e.g., bcrypt, PBKDF2)
- Enable multi-factor authentication (MFA)
- Implement OAuth 2.0/OpenID Connect protocols
- Add rate limiting and account lockout policies
- Implement comprehensive audit logging
- Use secure session management with idle timeouts
- Implement password complexity requirements and rotation policies

See [Microsoft's ASP.NET Core Security documentation](https://docs.microsoft.com/aspnet/core/security/) for production implementation guidance.

### User Roles

The application supports four role levels with hierarchical permissions:

- **Employee**: View and update assigned tasks, view projects where member, manage own profile
- **TeamLead**: All Employee permissions plus view team member activities
- **ProjectManager**: All TeamLead permissions plus create/manage projects, assign tasks
- **Administrator**: Full system access including all administrative functions

## Sample Data

The application includes pre-seeded data for testing:

**Users** (all available for mock login):

- `admin@contoso.com` - System Administrator (Administrator role)
- `camille.nicole@contoso.com` - Camille Nicole (Project Manager role)
- `floris.kregel@contoso.com` - Floris Kregel (Team Lead role)
- `ni.kang@contoso.com` - Ni Kang (Employee role)

**Project**:

- "ContosoDashboard Development" with 3 sample tasks in various states

## Application Pages

| Page | Route | Description | Auth Required |
|------|-------|-------------|---------------|
| Login | `/login` | User selection for mock auth | No |
| Dashboard | `/` | Summary, announcements, quick actions | Yes |
| Tasks | `/tasks` | View and manage your tasks | Yes |
| Projects | `/projects` | View your projects | Yes |
| Documents | `/documents` | Upload and browse documents | Yes |
| Project Details | `/projects/{id}` | Detailed project view | Yes (member only) |
| Team | `/team` | View team members | Yes |
| Notifications | `/notifications` | Manage notifications | Yes |
| Profile | `/profile` | Edit your profile | Yes |
| Logout | `/logout` | End session and clear cookies | Yes |

## Key Functionalities

### Dashboard (Home Page)

- Summary cards with real-time metrics
- Active announcements display
- Quick action links
- Recent notifications feed

### Task Management

- Filter by status, priority, and project
- Quick status updates via dropdown
- Priority-based color coding
- Overdue task highlighting

### Project Management

- Project cards with progress bars
- Completion percentage calculation
- Team member visibility
- Status badges

### User Profile

- Profile information editing
- Availability status management
- Notification preferences
- Display initials when no photo is set

<a id="documents"></a>
### Documents

Upload a file and see it listed immediately. Documents are visible to you if you uploaded them,
if they belong to a project you are on, if they were shared with you or your department, or if
you are an administrator.

- **Storage**: files are written to `ContosoDashboard/AppData/uploads/{userId}/{projectId|personal}/`,
  outside `wwwroot`, so they can never be served as static files. The database stores a relative
  path only.
- **Stored names**: each file is saved under a fresh GUID name, so an uploaded name can never
  overwrite an existing file or influence the path on disk.
- **Accepted types**: Office, PDF, text, CSV, and images, up to 25 MB. Both limits are set in
  `appsettings.json` under `DocumentStorage` and enforced on the server, not just in the browser.
- **Content check**: PDF, PNG, and JPEG uploads are verified against their expected leading
  bytes, so an executable renamed to `.pdf` is rejected.
- **Audit**: every upload writes a `DocumentActivity` row. These rows keep a copy of the title
  and deliberately have no foreign key to `Documents`, so history survives deletion.

## Troubleshooting

### Page Is Stuck on "Loading..." and No Buttons Respond

This almost always means the **Blazor Server connection dropped**, not that a page is broken.

Blazor Server keeps the UI in the browser but the component state on the server, connected by a
SignalR WebSocket. If the server stops, the browser keeps showing the last render while nothing
is wired to it — so buttons, filters, spinners, and even Logout all go dead at once. A page that
is still showing a loading spinner will show it forever, because the code that replaces it runs
on the server.

- The app shows a "Connection lost. Reconnecting..." overlay when this happens. If you see it,
  the server went away.
- If the server was restarted, the browser cannot resume the old session and will offer a
  **Reload** link. Reloading restores the page; your login cookie survives.
- Confirm the server is actually running: `Get-NetTCPConnection -LocalPort 5000 -State Listen`.
  If nothing is returned, start it with `dotnet run`.

**Symptom that all controls die simultaneously is the tell.** A genuine bug in one page normally
breaks that page only, and usually raises an error rather than freezing everything.

### Can't Login

- Ensure database is created (run `dotnet run` to auto-create)
- Check that seeded users exist in database
- Clear browser cookies and try again

### Redirected to Login After Login

- Check browser cookies are enabled
- Clear browser cache and cookies
- Try incognito/private mode

### Can't Access a Page

- Verify you're logged in (user name shown in top-right)
- Check if your role has permission for that resource
- Verify you're a member of the project/task you're trying to access

### Database Issues

**Option 1: Delete the SQLite database file**

```powershell
Remove-Item ContosoDashboard.db*
# Then run the application - database will be recreated automatically
```

**Option 2: Using EF Tools**

- Delete database: `dotnet ef database drop --force`
- Recreate: Run application (auto-creates with seed data)

**Note**: The application uses `EnsureCreated()` for development, so just running `dotnet run` will automatically create and seed the database if it doesn't exist.

## Security Concepts and Patterns

This training application demonstrates the following security concepts and patterns:

1. **Authentication Patterns** - How to implement and configure authentication
2. **Authorization Enforcement** - Using attributes and policies
3. **Claims-Based Identity** - Working with user claims
4. **IDOR Prevention** - Service-level authorization checks
5. **Security Best Practices** - Defense in depth, least privilege
6. **ASP.NET Core Security** - Industry-standard patterns and middleware

## Known Limitations (Training Context)

This is a **training application**, not production code. Known limitations include:

- **Mock authentication**: No real passwords - anyone can select any user account
- **No rate limiting**: Vulnerable to brute force attacks and denial of service
- **No audit logging**: Security events (login, failed auth, data changes) are not logged
- **Simplified input validation**: Production apps need more comprehensive validation
- **No session timeout warnings**: Users aren't warned before session expiration
- **CSP includes unsafe directives**: `'unsafe-inline'` and `'unsafe-eval'` required for Blazor Server but not ideal for security
- **No email verification**: User emails are not validated
- **No account lockout**: Failed login attempts don't trigger account locks
- **No real virus scanning**: `PermissiveFileScanner` accepts every file it is given. It exists so
  the scanning step is a real seam in the upload flow, not so uploads are actually safe. The app
  must run fully offline, so no scanning engine or cloud service is used. Extension, size, and
  magic-number checks are the only real defences. See `specs/001-document-management/research.md`
  (R-002, R-014) for the production architecture this stands in for.
- **No document deletion, sharing, search, preview, or download UI yet**: the MVP covers upload
  and listing only. Those remain as tasks T046-T108.

These limitations are **intentional** for training purposes to keep the application simple and self-contained. Production applications must address all of these security concerns.

## Code Quality Features

The application demonstrates good coding practices:

- Database indexes on frequently queried fields for performance
- Async/await pattern throughout for non-blocking operations
- Entity Framework Core with eager loading (`.Include()`) to prevent N+1 query problems
- Clean separation of concerns (Models, Services, Data, Pages)
- Dependency injection for loose coupling and testability
