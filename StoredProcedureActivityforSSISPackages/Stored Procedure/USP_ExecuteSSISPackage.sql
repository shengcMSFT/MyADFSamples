SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		shengc
-- Create date:	5/11/2017
-- Description:	The sample stored procedure to call SSIS package in Synchronized mode and return failure if the package execution fails. 
-- =============================================
CREATE PROCEDURE USP_ExecuteSSISPackage 
	
	@package_name nvarchar(260), 
	@folder_name nvarchar(128), 
	@project_name nvarchar(128)
AS
BEGIN
	SET NOCOUNT ON;
	    
	DECLARE @execution_id bigint
	DECLARE @errstr nvarchar(150)
	EXEC [SSISDB].[catalog].[create_execution] @folder_name, @project_name, @package_name, @use32bitruntime=False, @execution_id=@execution_id OUTPUT
	DECLARE @var0 smallint = 1
	EXEC [SSISDB].[catalog].[set_execution_parameter_value] @execution_id,  @object_type=50, @parameter_name=N'SYNCHRONIZED', @parameter_value=@var0
	EXEC [SSISDB].[catalog].[start_execution] @execution_id
	IF (SELECT [status] FROM [SSISDB].[catalog].[executions] WHERE execution_id = @execution_id)<>7 -- 7 is success, returning all none success runs as error of this stored procedure execution. 
		BEGIN
			SET @errstr=N'The SSIS package execution with execution ID '+ CAST(@execution_id as nvarchar(20)) + N' failed. For error details, please check the SSIS catalog database.' --return execution ID for failed run
			RAISERROR(@errstr, 15, 1)
		END

END
GO
