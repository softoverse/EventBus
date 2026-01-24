# Contributing to Softoverse.EventBus.InMemory

Thank you for your interest in contributing! This document provides guidelines and instructions for contributing to the project.

## 🎯 How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, include:

- **Clear title**: Describe the issue concisely
- **Description**: Detailed explanation of the problem
- **Steps to reproduce**: Numbered steps to reproduce the behavior
- **Expected behavior**: What you expected to happen
- **Actual behavior**: What actually happened
- **Environment**: OS, .NET version, package version
- **Code samples**: Minimal reproducible example
- **Screenshots**: If applicable

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. Include:

- **Clear title**: Describe the enhancement
- **Detailed description**: Explain the feature and its benefits
- **Use cases**: Real-world scenarios where this would be useful
- **Alternative solutions**: Other approaches you've considered
- **Examples**: Code samples showing how the feature would work

### Pull Requests

1. **Fork the repository** and create your branch from `main`
2. **Make your changes** following our coding standards
3. **Add tests** for new features or bug fixes
4. **Update documentation** if you're changing APIs
5. **Ensure tests pass** and code builds successfully
6. **Write meaningful commit messages** following our commit conventions
7. **Submit the pull request** with a clear description

## 🛠️ Development Setup

### Prerequisites

- .NET 10 SDK or later
- Git
- Your favorite IDE (Visual Studio, Rider, VS Code)

### Getting Started

```bash
# Clone your fork
git clone https://github.com/YOUR-USERNAME/EventBus.git
cd EventBus

# Add upstream remote
git remote add upstream https://github.com/mahmudabir/EventBus.git

# Create a feature branch
git checkout -b feature/your-feature-name

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests (if available)
dotnet test
```

## 📝 Coding Standards

### C# Style Guidelines

- Follow [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and small
- Use async/await for I/O operations
- Prefer immutability where possible

### Example

```csharp
/// <summary>
/// Processes the specified event asynchronously.
/// </summary>
/// <param name="event">The event to process.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public async Task ProcessEventAsync(IEvent @event, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(@event);
    
    _logger.LogInformation("Processing event {EventType}", @event.GetType().Name);
    
    await ProcessEventHandlersAsync(@event, cancellationToken).ConfigureAwait(false);
}
```

### File Organization

```
src/
  Softoverse.EventBus.InMemory/
    Abstractions/        # Interfaces and abstract classes
    Infrastructure/      # Implementation classes
      Channels/          # Channel-based implementation
      General/           # General implementation
    Models/              # Data models
      EventDispatch/     # Event dispatch models
      Settings/          # Configuration models
```

## 🧪 Testing Guidelines

### Unit Tests

- Test one thing per test
- Use descriptive test names: `MethodName_Scenario_ExpectedBehavior`
- Follow Arrange-Act-Assert pattern
- Mock external dependencies
- Test edge cases and error conditions

### Example

```csharp
[Fact]
public async Task PublishAsync_ValidEvent_EnqueuesEvent()
{
    // Arrange
    var eventBus = CreateEventBus();
    var testEvent = new TestEvent { Data = "test" };
    
    // Act
    await eventBus.PublishAsync(testEvent);
    
    // Assert
    // Verify event was enqueued
}

[Fact]
public async Task HandleAsync_NullEvent_ThrowsArgumentNullException()
{
    // Arrange
    var handler = new TestHandler();
    
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentNullException>(
        () => handler.HandleAsync(null!)
    );
}
```

## 📚 Documentation Standards

### XML Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Publishes an event to all registered handlers.
/// </summary>
/// <typeparam name="TEvent">The type of event to publish.</typeparam>
/// <param name="event">The event instance to publish.</param>
/// <param name="cancellationToken">Optional cancellation token.</param>
/// <returns>A ValueTask representing the asynchronous operation.</returns>
/// <exception cref="ArgumentNullException">Thrown when event is null.</exception>
ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    where TEvent : class, IEvent;
```

### README Updates

When adding new features:
- Update the Features section
- Add usage examples
- Update API Reference if needed
- Add to Changelog

## 🔀 Git Workflow

### Branching Strategy

- `main` - Stable production code
- `feature/*` - New features
- `bugfix/*` - Bug fixes
- `docs/*` - Documentation updates
- `refactor/*` - Code refactoring

### Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**

```
feat(eventbus): Add InvokeAsync method for request-response pattern

Adds support for synchronous request-response pattern where
handlers can return values. Useful for query scenarios.

Closes #123
```

```
fix(channel): Correct null check logic in PublishAsync

The null check was inverted (using != null!) causing incorrect
behavior. Changed to proper null check.

Fixes #456
```

### Keeping Your Fork Updated

```bash
# Fetch upstream changes
git fetch upstream

# Merge upstream/main into your main
git checkout main
git merge upstream/main

# Rebase your feature branch
git checkout feature/your-feature-name
git rebase main
```

## 🎨 Code Review Process

### For Contributors

- Respond to feedback promptly
- Make requested changes in new commits
- Don't force-push after review has started
- Be open to suggestions and discussions

### Review Checklist

- [ ] Code follows project style guidelines
- [ ] All tests pass
- [ ] New code has adequate test coverage
- [ ] Documentation is updated
- [ ] No breaking changes (or clearly documented)
- [ ] Commit messages are clear and meaningful
- [ ] No merge conflicts

## 📋 Pull Request Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-reviewed code
- [ ] Commented complex code
- [ ] Updated documentation
- [ ] No new warnings
- [ ] Added tests
- [ ] All tests pass
```

## 🐛 Issue Triage

### Priority Levels

- **Critical**: Security vulnerabilities, data loss, crashes
- **High**: Major features broken, significant performance issues
- **Medium**: Minor features broken, moderate bugs
- **Low**: Minor bugs, cosmetic issues, enhancements

### Labels

- `bug` - Something isn't working
- `enhancement` - New feature or request
- `documentation` - Documentation improvements
- `good first issue` - Good for newcomers
- `help wanted` - Extra attention needed
- `question` - Further information requested
- `wontfix` - Won't be fixed
- `duplicate` - Already reported

## 💬 Community Guidelines

### Code of Conduct

- Be respectful and inclusive
- Welcome newcomers
- Accept constructive criticism
- Focus on what's best for the project
- Show empathy towards others

### Communication

- Use clear, concise language
- Provide context and examples
- Be patient with responses
- Search before asking questions
- Stay on topic

## 🎓 Learning Resources

### Understanding the Codebase

- [System.Threading.Channels Documentation](https://docs.microsoft.com/en-us/dotnet/core/extensions/channels)
- [Dependency Injection in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Background Services](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)

### Design Patterns

- [Publish-Subscribe Pattern](https://en.wikipedia.org/wiki/Publish%E2%80%93subscribe_pattern)
- [Domain Events](https://martinfowler.com/eaaDev/DomainEvent.html)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)

## 🏆 Recognition

Contributors are recognized in:
- GitHub contributors list
- Release notes
- Project acknowledgments

Significant contributors may be invited to become maintainers.

## 📞 Questions?

- **GitHub Discussions**: For general questions and discussions
- **GitHub Issues**: For bug reports and feature requests
- **Pull Request Comments**: For code-specific questions

---

Thank you for contributing to Softoverse.EventBus.InMemory! 🎉
