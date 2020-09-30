using Dissertation.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace Dissertation.Services
{
    public class DissDatabaseContext : DbContext
    {
        public DbSet<Node> Nodes { get; set; }

        public DbSet<NodeEdge> NodeEdges { get; set; }

        public DbSet<Building> Buildings { get; set; }

        public DbSet<TimetableEvent> TimetableEvents { get; set; }

        public DissDatabaseContext(DbContextOptions<DissDatabaseContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Node>(entity =>
            {
                entity.HasKey(e => e.NodeId);

                entity.ToTable("Nodes");
                entity.Property(e => e.NodeId).HasColumnName("node_id").HasColumnType("varchar(20)");
                entity.Property(e => e.BuildingCode).HasColumnName("building_code").HasColumnType("varchar(10)");
                entity.Property(e => e.Floor).HasColumnName("floor").HasColumnType("tinyint");
                entity.Property(e => e.Type).HasColumnName("node_type").HasColumnType("tinyint");
                entity.Property(e => e.Latitude).HasColumnName("latitude").HasColumnType("float");
                entity.Property(e => e.Longitude).HasColumnName("longitude").HasColumnType("float");
                entity.Property(e => e.RoomName).HasColumnName("room_name").HasColumnType("varchar(255)");
                entity.Property(e => e.CorridorWidth).HasColumnName("corridor_width").HasColumnType("float");
                entity.Property(e => e.LeafletNodeType).HasColumnName("leaflet_type").HasColumnType("varchar(20)");

                entity.HasOne(node => node.Building).WithMany(building => building.Nodes).HasForeignKey(node => node.BuildingCode).HasConstraintName("FK_Nodes_Buildings");
            });

            modelBuilder.Entity<NodeEdge>(entity =>
            {
                entity.HasKey(e => e.RowId);

                entity.ToTable("Node_Edges");
                entity.Property(e => e.RowId).HasColumnName("row_id").UseIdentityColumn();
                entity.Property(e => e.Node1Id).HasColumnName("node1_id").HasColumnType("varchar(20)");
                entity.Property(e => e.Node2Id).HasColumnName("node2_id").HasColumnType("varchar(20)");
                entity.Property(e => e.Weight).HasColumnName("weight").HasColumnType("float");
                entity.Property(e => e.CorridorArea).HasColumnName("corridor_area").HasColumnType("float");

                entity.HasOne(nodeEdge => nodeEdge.Node1).WithMany(node => node.OutgoingEdges).HasForeignKey(nodeEdge => nodeEdge.Node1Id).HasConstraintName("FK_Node_Edges_Nodes");
                entity.HasOne(nodeEdge => nodeEdge.Node2).WithMany(node => node.IncomingEdges).HasForeignKey(nodeEdge => nodeEdge.Node2Id).HasConstraintName("FK_Node_Edges_Nodes1");
            });

            modelBuilder.Entity<Building>(entity =>
            {
                entity.HasKey(e => e.BuildingCode);

                entity.ToTable("Buildings");
                entity.Property(e => e.BuildingCode).HasColumnName("building_code").HasColumnType("varchar(10)");
                entity.Property(e => e.BuildingName).HasColumnName("building_name").HasColumnType("varchar(100)");
            });

            modelBuilder.Entity<TimetableEvent>(entity =>
            {
                entity.HasKey(e => e.RowId);

                entity.ToTable("Timetable_Events");
                entity.Property(e => e.RowId).HasColumnName("row_id").UseIdentityColumn();
                entity.Property(e => e.StudentId).HasColumnName("student_id").HasColumnType("int");
                entity.Property(e => e.EventDate).HasColumnName("event_date").HasColumnType("date");
                entity.Property(e => e.StartTime).HasColumnName("start_time").HasColumnType("time(7)");
                entity.Property(e => e.EndTime).HasColumnName("end_time").HasColumnType("time(7)");
                entity.Property(e => e.LocationNodeId).HasColumnName("location_node_id").HasColumnType("varchar(20)");

                entity.HasOne(evnt => evnt.LocationNode).WithMany(node => node.Events).HasForeignKey(evnt => evnt.LocationNodeId).HasConstraintName("FK_Timetable_Events_Nodes");
            });
        }
    }
}