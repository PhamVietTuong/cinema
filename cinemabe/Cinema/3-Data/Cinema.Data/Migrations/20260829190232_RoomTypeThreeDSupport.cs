using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cinema.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoomTypeThreeDSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SupportsThreeD",
                table: "RoomType",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "ThreeDSurcharge",
                table: "RoomType",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            // ProjectionForm dropped its IMAX member: it named a venue brand, which is the room
            // class's axis, not the image dimension's. Rows holding the retired value 3 become 2D —
            // an IMAX screening is 2D unless it was explicitly booked as 3D.
            migrationBuilder.Sql("UPDATE [ShowTime] SET [ProjectionForm] = 1 WHERE [ProjectionForm] = 3;");

            // Backfill from evidence rather than from names: any room class that already hosts a 3D
            // showtime demonstrably has a 3D projector. Without this every existing 3D showtime would
            // fail the new room-class guard the moment an admin edited it.
            migrationBuilder.Sql(@"
                UPDATE rt
                SET    rt.[SupportsThreeD] = 1
                FROM   [RoomType] rt
                WHERE  EXISTS (
                           SELECT 1
                           FROM   [Room] r
                           JOIN   [ShowTimeRoom] sr ON sr.[RoomId] = r.[Id]
                           JOIN   [ShowTime] st     ON st.[Id] = sr.[ShowTimeId]
                           WHERE  r.[RoomTypeId] = rt.[Id]
                             AND  st.[ProjectionForm] = 2
                       );");

            // The four class names the reference seed ships with are unambiguous, so give them
            // working defaults — insert_db.sql runs before this migration and cannot set them.
            // Surcharges are indicative VND figures; an operator edits them per theater afterwards.
            migrationBuilder.Sql(@"
                UPDATE [RoomType]
                SET    [SupportsThreeD] = 1,
                       [ThreeDSurcharge] = CASE [Name] WHEN N'3D' THEN 30000 ELSE 40000 END
                WHERE  [Name] IN (N'3D', N'IMAX', N'4DX')
                  AND  [SupportsThreeD] = 0
                  AND  [ThreeDSurcharge] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportsThreeD",
                table: "RoomType");

            migrationBuilder.DropColumn(
                name: "ThreeDSurcharge",
                table: "RoomType");
        }
    }
}
