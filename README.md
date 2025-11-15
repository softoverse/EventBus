# CqrsKit

CqrsKit is a package that can be used for maintaining CQRS design pattern in any kind of project with dependency
injection.

Because CqrsKit uses dependency injection to overcome the dynamic execution under the hood. So, there is minimal
overhead at runtime.
Also, this package has execution filter feature just like Asp.Net's ActionFilter feature allowing you to invoke
something before and after the executions.

```bash
dotnet add package Softoverse.CqrsKit
```

# Future Improvements

<ul>
    <li>
        Currently, supports Execution filter which is implemented for one implementation for all commands and same for queries.
        <strong>We are planning to implement a feature like when you want to different execution filter from the common execution filters, user can explicitly implement it by declaring execution filter for any command explicitly.</strong> 
    </li>
</ul>

# Proper documentation is coming soon...