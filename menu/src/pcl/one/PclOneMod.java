package pcl.one;

import java.lang.reflect.Method;

import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.screen.v1.ScreenEvents;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.components.events.GuiEventListener;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.client.gui.screens.TitleScreen;
import net.minecraft.network.chat.Component;

/**
 * Adds a "PCL One" button to the vanilla title screen. Everything else lives in
 * {@link PclOneScreen}; no mixins, no third-party launcher UI.
 */
public final class PclOneMod implements ClientModInitializer
{
    private static Method addRenderableWidget;

    @Override
    public void onInitializeClient()
    {
        try
        {
            addRenderableWidget = Screen.class.getDeclaredMethod("addRenderableWidget", GuiEventListener.class);
            addRenderableWidget.setAccessible(true);
        }
        catch (Throwable ignored)
        {
            // If the method ever changes the menu still opens from the key binding below.
        }

        ScreenEvents.AFTER_INIT.register((client, screen, scaledWidth, scaledHeight) ->
        {
            if (!(screen instanceof TitleScreen)) return;

            Button button = Button.builder(Component.literal("PCL One"), b ->
            {
                try
                {
                    client.setScreenAndShow(new PclOneScreen());
                }
                catch (Throwable t)
                {
                    Diag.log("open-menu", t);
                }
            })
                .bounds(scaledWidth - 108, 6, 102, 20)
                .build();

            if (addRenderableWidget != null)
            {
                try
                {
                    addRenderableWidget.invoke(screen, button);
                }
                catch (Throwable ignored)
                {
                }
            }
        });
    }
}
