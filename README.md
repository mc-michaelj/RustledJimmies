# Oracle SQL Optimizer

A Windows Forms application that uses AI to analyze and optimize Oracle SQL procedures, with a focus on performance improvements and best practices.

## Features

- **AI-Powered Analysis**: Uses Google's Gemini AI to analyze SQL procedures and suggest optimizations
- **Performance Testing**: Executes both original and optimized procedures with timing comparisons
- **Smart Table Management**: Automatically handles table dependencies and data cleanup
- **Transaction Safety**: All operations are performed within transactions with automatic rollback
- **Detailed Reporting**: Shows execution times and data comparison between original and optimized versions

## Process Flow

1. **Input & Analysis**

   - User provides a SQL procedure for analysis
   - Application extracts table dependencies and schema information
   - AI analyzes the code for potential optimizations

2. **Table Management**

   - Identifies all tables referenced in the procedure
   - Queries Oracle's data dictionary for foreign key relationships
   - Performs topological sort to determine correct deletion order
   - Handles schema-qualified table names for accurate operations

3. **Testing Process**

   - Creates a transaction for safe testing
   - Clears table data in dependency order
   - Inserts test data if provided
   - Executes both original and optimized procedures
   - Measures execution time for each version
   - Validates results using provided validation queries
   - Automatically rolls back all changes

4. **Results Display**
   - Shows execution times for both versions
   - Displays data comparison between original and optimized results
   - Presents AI-suggested optimizations
   - Highlights performance improvements

## Implementation Details

### Table Dependency Management

- Uses Oracle's `ALL_CONSTRAINTS` view to identify foreign key relationships
- Implements Kahn's algorithm for topological sorting
- Handles cyclic dependencies with clear error reporting
- Supports schema-qualified table names

### Transaction Safety

- All operations run within `READ COMMITTED` transactions
- Automatic rollback after testing
- Exception handling with proper cleanup
- Maintains database consistency

### AI Integration

- Uses Google's Gemini AI for code analysis
- Structured prompts for consistent responses
- JSON-based schema extraction
- Performance-focused optimization suggestions

## Requirements

- .NET 8.0
- Oracle Database Client
- Google Cloud API Key (for Gemini AI)
- Windows OS

## Configuration

1. Set up your Google Cloud API key in the application settings
2. Configure Oracle connection string with appropriate credentials
3. Ensure proper database permissions for:
   - Table access
   - Constraint information
   - Data manipulation

## Usage

1. Launch the application
2. Enter your Oracle connection string
3. Paste the SQL procedure to analyze
4. (Optional) Provide test data and validation queries
5. Click "Analyze & Optimize"
6. Review the results and suggested optimizations

## Error Handling

The application handles various scenarios:

- Missing tables or views
- Foreign key constraint violations
- Cyclic dependencies
- Invalid SQL syntax
- AI service unavailability

## Best Practices

- Always provide schema-qualified table names
- Include appropriate test data
- Use specific validation queries
- Review AI suggestions before applying
- Test optimizations in a safe environment

## Security Considerations

- API keys are stored securely
- Database credentials are handled safely
- All operations are transactional
- No permanent data modifications during testing

## Performance Considerations

- Efficient table dependency resolution
- Optimized database queries
- Minimal memory footprint
- Fast execution time comparison

## Future Improvements

- Support for multiple AI models
- Enhanced performance metrics
- Batch processing capabilities
- Custom optimization rules
- Export/import functionality
