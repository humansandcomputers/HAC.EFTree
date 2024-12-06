--select * from Items
--where [Left] > -9 and [Right] < 16

--SELECT 
-- [Name], [Left], [Right],
--  LAG([Left]) OVER (ORDER BY [Left]) AS last_left
--FROM 
--  [Items]
--  Where [Left] > -9 and [Right] < 16 and last_left > 0

--WITH RecursiveCTE AS (
--    SELECT Id, nextRecord, Name
--    FROM [Table]
--    WHERE Name = 'E1' -- Replace 'E1' with the starting point
    
--    UNION ALL
    
--    SELECT t.Id, t.nextRecord, t.Name
--    FROM [Table] t
--    INNER JOIN RecursiveCTE r ON t.Id = r.nextRecord
--    WHERE r.nextRecord <> 0
--)

--SELECT *
--FROM RecursiveCTE;

WITH GetSibling AS 
(
    SELECT *
    FROM [Items]
    WHERE [Left] = -8

    UNION ALL
    
    SELECT t.*
    FROM [Items] t
    INNER JOIN GetSibling r 
	ON t.[Left] = r.[Right] + 1
)

SELECT *
FROM GetSibling;