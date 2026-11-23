import {
  Ban,
  CalendarCheck,
  CalendarClock,
  CircleOff,
  Hourglass,
  Inbox,
  type LucideIcon,
  MapPin,
  MapPinOff,
  MapPinX,
  MessageSquare,
  Navigation,
  Package,
  PackageCheck,
  PackageOpen,
  PackageSearch,
  Phone,
  PhoneOff,
  Plane,
  RefreshCw,
  RotateCcw,
  ThumbsDown,
  TriangleAlert,
  Truck,
  Undo2,
  UserCheck,
  Voicemail,
  Warehouse,
  X,
} from 'lucide-react';

/**
 * Pont entre le référentiel de statuts (packages/shared, sans dépendance React)
 * et les composants lucide. Le legacy stockait des classes Flaticon liées au
 * thème Metronic — le nom d'icône est ici une donnée, pas un détail de thème.
 */
const ICONS: Record<string, LucideIcon> = {
  Ban,
  CalendarCheck,
  CalendarClock,
  CircleOff,
  Hourglass,
  Inbox,
  MapPin,
  MapPinOff,
  MapPinX,
  MessageSquare,
  Navigation,
  Package,
  PackageCheck,
  PackageOpen,
  PackageSearch,
  Phone,
  PhoneOff,
  Plane,
  RefreshCw,
  RotateCcw,
  ThumbsDown,
  TriangleAlert,
  Truck,
  Undo2,
  UserCheck,
  Voicemail,
  Warehouse,
  X,
};

export function statusIcon(name: string): LucideIcon {
  return ICONS[name] ?? Package;
}
