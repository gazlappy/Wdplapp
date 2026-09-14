<?php
declare(strict_types=1);

/**
 * Discovers modules by scanning api/modules/<id>/Module.php.
 *
 * Discovery is by directory listing rather than a hardcoded list so deploying a
 * new module folder is all that is needed to enable it.
 */
final class Registry
{
    /** @var array<string, class-string<Module>>|null */
    private static ?array $modules = null;

    /** @return array<string, class-string<Module>> */
    public static function all(): array
    {
        if (self::$modules !== null) {
            return self::$modules;
        }

        $modules = [];
        $root = dirname(__DIR__) . '/modules';
        foreach (glob($root . '/*/Module.php') ?: [] as $file) {
            $id = basename(dirname($file));
            if (!preg_match('/^[a-z][a-z0-9_]*$/', $id)) {
                continue;
            }

            require_once $file;

            // modules/teams/Module.php defines TeamsModule.
            $class = str_replace(' ', '', ucwords(str_replace('_', ' ', $id))) . 'Module';
            if (!class_exists($class) || !is_subclass_of($class, Module::class)) {
                continue;
            }
            $modules[$class::id()] = $class;
        }

        ksort($modules);
        return self::$modules = $modules;
    }

    /** @return class-string<Module> */
    public static function find(string $id): string
    {
        $modules = self::all();
        if (!isset($modules[$id])) {
            throw new ApiError(404, 'unknown_module', "No such module: {$id}.");
        }
        return $modules[$id];
    }

    /** Ids and schema versions, for the app's deploy/health view. */
    public static function manifest(): array
    {
        $out = [];
        foreach (self::all() as $id => $class) {
            $out[] = [
                'id'            => $id,
                'title'         => $class::title(),
                'schemaVersion' => $class::schemaVersion(),
                'actions'       => array_keys($class::actions()),
            ];
        }
        return $out;
    }
}
