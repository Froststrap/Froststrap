package notification

// Maybe at some point we just make our own library
import "github.com/gen2brain/beeep"

var defaultIcon []byte

func Send(title string, message string) error {
	return beeep.Notify(title, message, defaultIcon)
}
